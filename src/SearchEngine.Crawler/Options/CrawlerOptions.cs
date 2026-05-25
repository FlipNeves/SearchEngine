namespace SearchEngine.Crawler.Options;

public sealed class CrawlerOptions
{
    public string[] SeedUrls { get; set; } = ["https://learn.microsoft.com/pt-br/dotnet/"];
    public int ConcurrentConsumers { get; set; } = 4;
    public int QueueCapacity { get; set; } = 10_000;
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int MaxPages { get; set; } = 500;
    public string UserAgent { get; set; } = "SearchEngineStudyBot/1.0 (+https://example.local)";
}
