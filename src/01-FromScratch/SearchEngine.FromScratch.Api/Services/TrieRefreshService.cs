using System.Diagnostics;
using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Core.Indexing;
using SearchEngine.FromScratch.Infrastructure.Daos;

namespace SearchEngine.FromScratch.Api.Services;

public sealed class TrieRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrieIndex _trieIndex;
    private readonly TrieRefreshOptions _options;
    private readonly ILogger<TrieRefreshService> _logger;

    public TrieRefreshService(
        IServiceScopeFactory scopeFactory,
        TrieIndex trieIndex,
        IOptions<TrieRefreshOptions> options,
        ILogger<TrieRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _trieIndex = trieIndex;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trie refresh failed; retrying in {Interval}", _options.RefreshInterval);
            }

            try
            {
                await Task.Delay(_options.RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var trie = new Trie(topN: _options.TopSuggestionsPerNode);

        using var scope = _scopeFactory.CreateScope();
        var dao = scope.ServiceProvider.GetRequiredService<IInvertedIndexDao>();

        var loaded = 0;
        await foreach (var doc in dao.StreamAllAsync(ct))
        {
            trie.Insert(doc.Term, doc.Postings.Count);
            loaded++;
        }

        _trieIndex.Replace(trie);
        _logger.LogInformation("Trie refreshed: {Words} words in {Elapsed} ms", loaded, sw.ElapsedMilliseconds);
    }
}
