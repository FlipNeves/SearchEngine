namespace SearchEngine.Api.Options;

public sealed class TrieRefreshOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int TopSuggestionsPerNode { get; set; } = 10;
    public int MaxSuggestionDistance { get; set; } = 2;
}
