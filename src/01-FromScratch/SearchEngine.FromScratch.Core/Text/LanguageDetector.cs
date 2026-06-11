namespace SearchEngine.FromScratch.Core.Text;

public readonly record struct LanguageGuess(string Language, bool Confident);

public sealed class LanguageDetector
{
    private const int ProfileSize = 320;
    private const int AbsentPenalty = ProfileSize;
    private const double MinMargin = 0.08;
    private const int MinGrams = 4;

    private readonly (string Lang, NGramProfile Profile)[] _languages;

    public LanguageDetector(IReadOnlyDictionary<string, string> trainingByLang)
        => _languages = trainingByLang
            .Select(kv => (kv.Key, NGramProfile.Build(kv.Value, ProfileSize)))
            .ToArray();

    public static LanguageDetector Default() => new(LanguageSamples.ByLang);

    public LanguageGuess Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _languages.Length == 0)
            return new LanguageGuess(LanguageSamples.Undetermined, false);

        var doc = NGramProfile.Build(text, ProfileSize);
        if (doc.TopGrams.Count < MinGrams)
            return new LanguageGuess(LanguageSamples.Undetermined, false);

        var ranked = _languages
            .Select(l => (l.Lang, Distance: Distance(doc, l.Profile)))
            .OrderBy(x => x.Distance)
            .ToArray();

        var best = ranked[0];
        if (ranked.Length == 1)
            return new LanguageGuess(best.Lang, true);

        var second = ranked[1];
        var confident = second.Distance > 0
            && second.Distance - best.Distance >= MinMargin * second.Distance;

        return new LanguageGuess(best.Lang, confident);
    }

    private static long Distance(NGramProfile doc, NGramProfile language)
    {
        long sum = 0;
        for (var textRank = 0; textRank < doc.TopGrams.Count; textRank++)
        {
            var langRank = language.RankOf(doc.TopGrams[textRank]);
            sum += langRank.HasValue ? Math.Abs(textRank - langRank.Value) : AbsentPenalty;
        }
        return sum;
    }
}
