using System.Collections.Concurrent;
using System.Threading.Channels;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Options;
using SearchEngine.Crawler.Options;
using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;

namespace SearchEngine.Crawler.Workers;

public sealed class WebCrawlerWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebCrawlerWorker> _logger;
    private readonly CrawlerOptions _options;

    private readonly Channel<string> _channel;
    private readonly ConcurrentDictionary<string, byte> _visited = new(StringComparer.OrdinalIgnoreCase);
    private readonly IBrowsingContext _browsing = BrowsingContext.New(Configuration.Default);
    private int _pagesProcessed;

    public WebCrawlerWorker(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<CrawlerOptions> options,
        ILogger<WebCrawlerWorker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = false,
            SingleWriter = false
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var seed in _options.SeedUrls)
            await TryEnqueueAsync(seed, stoppingToken);

        var consumers = Enumerable.Range(0, _options.ConcurrentConsumers)
            .Select(i => Task.Run(() => ConsumeLoopAsync(i, stoppingToken), stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(consumers);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Crawler stopped after processing {Count} pages", _pagesProcessed);
        }
    }

    private async Task ConsumeLoopAsync(int workerId, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("crawler");

        await foreach (var url in _channel.Reader.ReadAllAsync(ct))
        {
            if (Interlocked.Increment(ref _pagesProcessed) > _options.MaxPages)
            {
                _channel.Writer.TryComplete();
                break;
            }

            try
            {
                await ProcessUrlAsync(url, client, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Worker {WorkerId} failed on {Url}", workerId, url);
            }
        }
    }

    private async Task ProcessUrlAsync(string url, HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Skipped {Url} with status {Status}", url, (int)response.StatusCode);
            return;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase))
            return;

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await _browsing.OpenAsync(req => req.Content(html), ct);

        var page = new WebPage
        {
            Url = url,
            Title = document.Title?.Trim() ?? string.Empty,
            Content = ExtractCleanText(document),
            CrawledAtUtc = DateTime.UtcNow
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var pages = scope.ServiceProvider.GetRequiredService<IPagesRepository>();
            var indexers = scope.ServiceProvider.GetServices<IPageIndexer>();

            await pages.AddAsync(page, ct);

            foreach (var indexer in indexers)
                await indexer.IndexAsync(page, ct);
        }

        _logger.LogInformation("Indexed {Url} ({TitleLen} chars title, {ContentLen} chars body)",
            url, page.Title.Length, page.Content.Length);

        await EnqueueLinksAsync(document, url, ct);
    }

    private async Task EnqueueLinksAsync(IDocument document, string baseUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return;

        foreach (var anchor in document.QuerySelectorAll("a[href]").OfType<IHtmlAnchorElement>())
        {
            if (TryResolveAbsolute(baseUri, anchor.GetAttribute("href"), out var absolute))
                await TryEnqueueAsync(absolute, ct);
        }
    }

    private ValueTask TryEnqueueAsync(string url, CancellationToken ct)
    {
        if (!_visited.TryAdd(url, 0)) return ValueTask.CompletedTask;

        if (!_channel.Writer.TryWrite(url))
            _logger.LogDebug("Queue full — dropped {Url}", url);

        return ValueTask.CompletedTask;
    }

    private static bool TryResolveAbsolute(Uri baseUri, string? href, out string absolute)
    {
        absolute = string.Empty;
        if (string.IsNullOrWhiteSpace(href)) return false;
        if (href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!Uri.TryCreate(baseUri, href, out var resolved)) return false;
        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps)
            return false;

        absolute = resolved.GetLeftPart(UriPartial.Path);
        return true;
    }

    private static string ExtractCleanText(IDocument document)
    {
        foreach (var node in document.QuerySelectorAll("script, style, noscript"))
            node.Remove();

        return document.Body?.TextContent?.Trim() ?? string.Empty;
    }
}
