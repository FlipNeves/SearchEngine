namespace SearchEngine.FromScratch.Core.Ranking;

public sealed class Bm25Scorer
{
    private readonly Bm25Options _options;

    public Bm25Scorer(Bm25Options options) => _options = options;

    public double Score(int tf, long df, long totalDocs, int docLength, double avgDocLength)
    {
        if (tf == 0 || docLength == 0 || avgDocLength == 0 || totalDocs == 0)
            return 0;

        var idf = Math.Log((totalDocs - df + 0.5) / (df + 0.5) + 1);
        var lengthNorm = 1 - _options.B + _options.B * docLength / avgDocLength;
        var saturation = tf * (_options.K1 + 1) / (tf + _options.K1 * lengthNorm);
        return idf * saturation;
    }
}
