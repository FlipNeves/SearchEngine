using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.FromScratch.Infrastructure.DataModels;

[CollectionName("inverted_index")]
public sealed class InvertedIndexDataModel
{
    [BsonId]
    public string Word { get; set; } = string.Empty;

    public HashSet<ObjectId> PageIds { get; set; } = new();
}
