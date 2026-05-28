using MongoDB.Bson.Serialization.Attributes;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.FromScratch.Infrastructure.DataModels;

[CollectionName("phrase_index")]
public sealed class PhraseIndexDataModel
{
    [BsonId]
    public string Phrase { get; set; } = string.Empty;

    public int Size { get; set; }

    public List<PostingDataModel> Postings { get; set; } = new();
}
