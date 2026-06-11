using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.Shared.Domain.Interfaces;

namespace SearchEngine.FromScratch.Api.Services;

// Spell correction over the in-memory vocabulary. Queries the BK-tree for candidates within
// MaxSuggestionDistance edits (Damerau-Levenshtein), then picks the best by a small heuristic:
// fewer edits win, ties broken toward longer shared prefix (typos usually keep the head).
public sealed class BkTreeSpellCorrector : ISpellCorrector
{
    private readonly VocabularyIndex _vocabulary;
    private readonly int _maxDistance;

    public BkTreeSpellCorrector(VocabularyIndex vocabulary, IOptions<TrieRefreshOptions> options)
    {
        _vocabulary = vocabulary;
        _maxDistance = options.Value.MaxSuggestionDistance;
    }

    public bool IsReady => _vocabulary.IsReady;

    public string? Correct(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || !_vocabulary.IsReady) return null;

        var query = term.Trim().ToLowerInvariant();

        string? best = null;
        var bestScore = int.MaxValue;
        var bestDistance = int.MaxValue;

        foreach (var (word, distance) in _vocabulary.Current.BkTree.Search(query, _maxDistance))
        {
            if (distance == 0) continue; // exact match: not an OOV correction

            var prefix = CommonPrefixLength(word, query);
            var score = distance * 10 - prefix * 2;

            if (score < bestScore || (score == bestScore && distance < bestDistance))
            {
                best = word;
                bestScore = score;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < length && a[i] == b[i]) i++;
        return i;
    }
}
