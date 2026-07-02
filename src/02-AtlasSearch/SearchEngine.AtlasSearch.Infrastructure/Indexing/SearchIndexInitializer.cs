using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SearchEngine.Shared.Persistence.DataModels;
using SearchEngine.Shared.Persistence.Internal;

namespace SearchEngine.AtlasSearch.Infrastructure.Indexing;

public sealed class SearchIndexInitializer : IHostedService
{
    private const string IndexDefinitionJson = """
        {
          "mappings": {
            "dynamic": false,
            "fields": {
              "title": { "type": "string" },
              "content": { "type": "string" },
              "language": { "type": "token" }
            }
          }
        }
        """;

    private readonly IMongoDatabase _database;
    private readonly AtlasSearchOptions _options;
    private readonly ILogger<SearchIndexInitializer> _logger;

    public SearchIndexInitializer(
        IMongoDatabase database,
        IOptions<AtlasSearchOptions> options,
        ILogger<SearchIndexInitializer> logger)
    {
        _database = database;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var pages = CollectionResolver.Resolve<WebPageDataModel>(_database);

        using var cursor = await pages.SearchIndexes.ListAsync(cancellationToken: cancellationToken);
        var existing = await cursor.ToListAsync(cancellationToken);
        if (existing.Any(i => i["name"].AsString == _options.IndexName))
        {
            _logger.LogInformation("Search index {IndexName} already exists", _options.IndexName);
            return;
        }

        var model = new CreateSearchIndexModel(_options.IndexName, BsonDocument.Parse(IndexDefinitionJson));
        await pages.SearchIndexes.CreateOneAsync(model, cancellationToken);
        _logger.LogInformation("Search index {IndexName} created; mongot build may take a minute", _options.IndexName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
