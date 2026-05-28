namespace SearchEngine.FromScratch.Core.Ranking;

public sealed class Bm25Options
{
    public double K1 { get; set; } = 1.2;
    public double B { get; set; } = 0.75;
    public double WTitle { get; set; } = 3.0;
    public double WContent { get; set; } = 1.0;
    public double PhraseBoost { get; set; } = 2.0;
}
