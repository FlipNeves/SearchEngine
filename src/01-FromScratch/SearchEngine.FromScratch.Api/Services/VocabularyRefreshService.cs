using System.Diagnostics;
using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Core.Indexing;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.Shared.Text;

namespace SearchEngine.FromScratch.Api.Services;

// Periodically rebuilds the in-memory vocabulary from the inverted index and swaps it in
// atomically. Builds the Trie and the BK-tree from the same stream, so they stay in sync.
public sealed class VocabularyRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VocabularyIndex _vocabulary;
    private readonly TrieRefreshOptions _options;
    private readonly ILogger<VocabularyRefreshService> _logger;

    public VocabularyRefreshService(
        IServiceScopeFactory scopeFactory,
        VocabularyIndex vocabulary,
        IOptions<TrieRefreshOptions> options,
        ILogger<VocabularyRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _vocabulary = vocabulary;
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
                _logger.LogError(ex, "Vocabulary refresh failed; retrying in {Interval}", _options.RefreshInterval);
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
        var bkTree = new BkTree(DamerauLevenshtein.Distance);

        using var scope = _scopeFactory.CreateScope();
        var dao = scope.ServiceProvider.GetRequiredService<IInvertedIndexDao>();

        var loaded = 0;
        await foreach (var doc in dao.StreamAllAsync(ct))
        {
            trie.Insert(doc.Term, doc.Postings.Count);
            bkTree.Add(doc.Term);
            loaded++;
        }

        _vocabulary.Replace(new VocabularySnapshot { Trie = trie, BkTree = bkTree });
        _logger.LogInformation("Vocabulary refreshed: {Words} words in {Elapsed} ms", loaded, sw.ElapsedMilliseconds);
    }
}
