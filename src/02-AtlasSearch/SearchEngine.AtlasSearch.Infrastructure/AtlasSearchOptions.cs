namespace SearchEngine.AtlasSearch.Infrastructure;

public sealed class AtlasSearchOptions
{
    public string IndexName { get; set; } = "pages_search";
    public double WTitle { get; set; } = 3.0;
    public int FuzzyMaxEdits { get; set; } = 1;
    public int FuzzyPrefixLength { get; set; } = 1;
    public double FuzzyFallbackBoost { get; set; } = 0.3;
    public int PhraseSlop { get; set; } = 2;
    public double PhraseBoost { get; set; } = 2.0;
    public double LanguageBoost { get; set; } = 2.0;
}
