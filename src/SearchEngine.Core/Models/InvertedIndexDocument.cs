using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SearchEngine.Core.Models;

public sealed class InvertedIndexDocument
{
    [BsonId]
    [BsonElement("word")]
    public string Word { get; set; } = string.Empty;

    [BsonElement("pageIds")]
    public HashSet<ObjectId> PageIds { get; set; } = new();
}
