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
              "title": [
                { "type": "string" },
                { "type": "autocomplete" }
              ],
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
        var definition = BsonDocument.Parse(IndexDefinitionJson);

        using var cursor = await pages.SearchIndexes.ListAsync(cancellationToken: cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);
        var existing = indexes.FirstOrDefault(i => i["name"].AsString == _options.IndexName);

        if (existing is null)
        {
            var model = new CreateSearchIndexModel(_options.IndexName, definition);
            await pages.SearchIndexes.CreateOneAsync(model, cancellationToken);
            _logger.LogInformation("Search index {IndexName} created; mongot build may take a minute", _options.IndexName);
            return;
        }

        if (HasAutocompleteOnTitle(existing))
        {
            _logger.LogInformation("Search index {IndexName} already up to date", _options.IndexName);
            return;
        }

        await pages.SearchIndexes.UpdateAsync(_options.IndexName, definition, cancellationToken);
        _logger.LogInformation("Search index {IndexName} updated with autocomplete mapping; mongot rebuild may take a minute", _options.IndexName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool HasAutocompleteOnTitle(BsonValue index)
    {
        if (!index.AsBsonDocument.TryGetValue("latestDefinition", out var definition)
            || !definition.AsBsonDocument.TryGetValue("mappings", out var mappings)
            || !mappings.AsBsonDocument.TryGetValue("fields", out var fields)
            || !fields.AsBsonDocument.TryGetValue("title", out var title))
            return false;

        var titleMappings = title is BsonArray array ? array : new BsonArray { title };
        return titleMappings
            .OfType<BsonDocument>()
            .Any(m => m.TryGetValue("type", out var type) && type == "autocomplete");
    }
}
