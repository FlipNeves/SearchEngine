using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.Shared.Domain.Interfaces;

namespace SearchEngine.FromScratch.Api.Services;

// Scan linear sobre o vocabulário em memória do Trie. Vive na API porque o vocabulário
// (TrieIndex) é um serviço desta camada; a BK-tree da Fase 4b substitui o scan aqui dentro.
public sealed class TrieSpellCorrector : ISpellCorrector
{
    private readonly TrieIndex _trieIndex;
    private readonly int _maxDistance;

    public TrieSpellCorrector(TrieIndex trieIndex, IOptions<TrieRefreshOptions> options)
    {
        _trieIndex = trieIndex;
        _maxDistance = options.Value.MaxSuggestionDistance;
    }

    public bool IsReady => _trieIndex.IsReady;

    public string? Correct(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || !_trieIndex.IsReady) return null;

        var query = term.Trim().ToLowerInvariant();

        string? best = null;
        var bestScore = int.MaxValue;
        var bestDistance = int.MaxValue;

        foreach (var word in _trieIndex.Current.AllWords())
        {
            if (Math.Abs(word.Length - query.Length) > _maxDistance) continue;

            var distance = Levenshtein.Distance(word, query);
            if (distance == 0 || distance > _maxDistance) continue;

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
