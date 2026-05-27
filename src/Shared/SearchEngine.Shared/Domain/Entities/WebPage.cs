namespace SearchEngine.Shared.Domain.Entities;

public sealed class WebPage
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CrawledAtUtc { get; set; }
}
