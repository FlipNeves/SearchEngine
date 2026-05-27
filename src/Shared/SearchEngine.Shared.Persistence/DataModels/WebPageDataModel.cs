using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.Shared.Persistence.DataModels;

[CollectionName("pages")]
public sealed class WebPageDataModel
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CrawledAtUtc { get; set; }
}
