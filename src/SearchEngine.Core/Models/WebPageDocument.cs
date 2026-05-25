using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SearchEngine.Core.Models;

public sealed class WebPageDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("crawledAtUtc")]
    public DateTime CrawledAtUtc { get; set; }
}
