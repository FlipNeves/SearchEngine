using System.Diagnostics;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SearchEngine.Api.Options;
using SearchEngine.Core.Indexing;
using SearchEngine.Core.Models;
using SearchEngine.Infrastructure.Mongo;

namespace SearchEngine.Api.Services;

public sealed class TrieRefreshService : BackgroundService
{
    private readonly IMongoCollection<InvertedIndexDocument> _index;
    private readonly TrieIndex _trieIndex;
    private readonly TrieRefreshOptions _options;
    private readonly ILogger<TrieRefreshService> _logger;

    public TrieRefreshService(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        TrieIndex trieIndex,
        IOptions<TrieRefreshOptions> options,
        ILogger<TrieRefreshService> logger)
    {
        var db = client.GetDatabase(mongoOptions.Value.Database);
        _index = db.GetCollection<InvertedIndexDocument>(mongoOptions.Value.InvertedIndexCollection);
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

        using var cursor = await _index
            .Find(FilterDefinition<InvertedIndexDocument>.Empty)
            .ToCursorAsync(ct);

        var loaded = 0;
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var doc in cursor.Current)
            {
                trie.Insert(doc.Word, doc.PageIds.Count);
                loaded++;
            }
        }

        _trieIndex.Replace(trie);
        _logger.LogInformation("Trie refreshed: {Words} words in {Elapsed} ms", loaded, sw.ElapsedMilliseconds);
    }
}
