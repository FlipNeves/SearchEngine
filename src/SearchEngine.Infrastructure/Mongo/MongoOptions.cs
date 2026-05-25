namespace SearchEngine.Infrastructure.Mongo;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "searchengine";
    public string PagesCollection { get; set; } = "pages";
    public string InvertedIndexCollection { get; set; } = "inverted_index";
}
