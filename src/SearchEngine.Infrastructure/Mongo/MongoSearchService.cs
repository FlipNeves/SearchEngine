using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SearchEngine.Core.Abstractions;
using SearchEngine.Core.Models;
using SearchEngine.Core.Text;

namespace SearchEngine.Infrastructure.Mongo;

public sealed class MongoSearchService : ISearchService
{
    private readonly IMongoCollection<WebPageDocument> _pages;
    private readonly IMongoCollection<InvertedIndexDocument> _index;
    private readonly ILogger<MongoSearchService> _logger;

    public MongoSearchService(
        IMongoClient client,
        IOptions<MongoOptions> options,
        ILogger<MongoSearchService> logger)
    {
        var opts = options.Value;
        var database = client.GetDatabase(opts.Database);
        _pages = database.GetCollection<WebPageDocument>(opts.PagesCollection);
        _index = database.GetCollection<InvertedIndexDocument>(opts.InvertedIndexCollection);
        _logger = logger;
    }

    public async Task IndexPageAsync(WebPageDocument page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        await _pages.ReplaceOneAsync(
            p => p.Id == page.Id,
            page,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        var tokens = Tokenizer
            .Tokenize($"{page.Title} {page.Content}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0) return;

        var ops = new List<WriteModel<InvertedIndexDocument>>(tokens.Length);
        foreach (var token in tokens)
        {
            var filter = Builders<InvertedIndexDocument>.Filter.Eq(d => d.Word, token);
            var update = Builders<InvertedIndexDocument>.Update
                .SetOnInsert(d => d.Word, token)
                .AddToSet(d => d.PageIds, page.Id);

            ops.Add(new UpdateOneModel<InvertedIndexDocument>(filter, update) { IsUpsert = true });
        }

        try
        {
            await _index.BulkWriteAsync(ops, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }
        catch (MongoBulkWriteException<InvertedIndexDocument> ex)
        {
            _logger.LogWarning(ex, "Partial bulk write for page {PageId}: {Errors} errors", page.Id, ex.WriteErrors.Count);
        }
    }

    public async Task<IReadOnlyList<WebPageDocument>> ExecuteSearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var tokens = Tokenizer
            .Tokenize(query)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0) return Array.Empty<WebPageDocument>();

        var indexFilter = Builders<InvertedIndexDocument>.Filter.In(d => d.Word, tokens);
        var entries = await _index.Find(indexFilter).ToListAsync(cancellationToken);

        if (entries.Count == 0) return Array.Empty<WebPageDocument>();

        var pageIds = entries.SelectMany(e => e.PageIds).ToHashSet();

        var pagesFilter = Builders<WebPageDocument>.Filter.In(p => p.Id, pageIds);
        return await _pages.Find(pagesFilter).ToListAsync(cancellationToken);
    }
}
