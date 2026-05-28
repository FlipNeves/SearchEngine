using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.FromScratch.Infrastructure.DataModels;

[CollectionName("inverted_index")]
public sealed class InvertedIndexDataModel
{
    [BsonId]
    public string Term { get; set; } = string.Empty;

    public List<PostingDataModel> Postings { get; set; } = new();
}

public sealed class PostingDataModel
{
    public ObjectId DocId { get; set; }
    public int TfTitle { get; set; }
    public int TfContent { get; set; }
    public List<PositionDataModel> PositionsTitle { get; set; } = new();
    public List<PositionDataModel> PositionsContent { get; set; } = new();
}

public sealed class PositionDataModel
{
    public int Start { get; set; }
    public int End { get; set; }
}
