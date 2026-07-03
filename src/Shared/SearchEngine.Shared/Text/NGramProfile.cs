using System.Text;

namespace SearchEngine.Shared.Text;

public sealed class NGramProfile
{
    public const char Boundary = '_';
    private const int MinN = 1;
    private const int MaxN = 3;

    private readonly Dictionary<string, int> _rankByGram;

    private NGramProfile(Dictionary<string, int> rankByGram, IReadOnlyList<string> topGrams)
    {
        _rankByGram = rankByGram;
        TopGrams = topGrams;
    }

    public IReadOnlyList<string> TopGrams { get; }

    public int? RankOf(string gram) => _rankByGram.TryGetValue(gram, out var rank) ? rank : null;

    public static NGramProfile Build(string text, int topN)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var word in Words(text))
        {
            var padded = Boundary + word + Boundary;
            for (var n = MinN; n <= MaxN; n++)
                for (var i = 0; i + n <= padded.Length; i++)
                {
                    var gram = padded.Substring(i, n);
                    counts.TryGetValue(gram, out var c);
                    counts[gram] = c + 1;
                }
        }

        var ordered = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topN)
            .Select(kv => kv.Key)
            .ToArray();

        var rankByGram = new Dictionary<string, int>(ordered.Length, StringComparer.Ordinal);
        for (var i = 0; i < ordered.Length; i++) rankByGram[ordered[i]] = i;

        return new NGramProfile(rankByGram, ordered);
    }

    private static IEnumerable<string> Words(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
                sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0)
            {
                yield return sb.ToString();
                sb.Clear();
            }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
