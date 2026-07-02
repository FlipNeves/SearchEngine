namespace SearchEngine.AtlasSearch.Infrastructure;

public sealed class AtlasSearchOptions
{
    public string IndexName { get; set; } = "pages_search";
    public double WTitle { get; set; } = 3.0;
    public int FuzzyMaxEdits { get; set; } = 1;
    public int FuzzyPrefixLength { get; set; } = 1;
}
