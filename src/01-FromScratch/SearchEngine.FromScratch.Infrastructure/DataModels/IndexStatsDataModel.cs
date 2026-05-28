using MongoDB.Bson.Serialization.Attributes;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.FromScratch.Infrastructure.DataModels;

[CollectionName("index_stats")]
public sealed class IndexStatsDataModel
{
    public const string GlobalId = "global";

    [BsonId]
    public string Id { get; set; } = GlobalId;

    public long TotalDocs { get; set; }
    public double AvgLengthTitle { get; set; }
    public double AvgLengthContent { get; set; }
}
