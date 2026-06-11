using SearchEngine.FromScratch.Core.Indexing;
using SearchEngine.FromScratch.Core.Text;

namespace SearchEngine.FromScratch.Api.Services;

// Immutable view of the in-memory vocabulary built from one refresh: the prefix Trie
// (autocomplete) and the BK-tree (fuzzy / spell-correction). Both always reflect the SAME
// vocabulary snapshot, so a reader can never see a new Trie paired with a stale BK-tree.
public sealed class VocabularySnapshot
{
    public required Trie Trie { get; init; }
    public required BkTree BkTree { get; init; }

    public static VocabularySnapshot Empty { get; } = new()
    {
        Trie = new Trie(),
        BkTree = new BkTree(DamerauLevenshtein.Distance),
    };
}

// Singleton holder. Replace swaps a single reference, so readers see either the whole old
// snapshot or the whole new one — never a torn mix. `volatile` publishes the swap promptly.
public sealed class VocabularyIndex
{
    private volatile VocabularySnapshot _snapshot = VocabularySnapshot.Empty;

    public VocabularySnapshot Current => _snapshot;
    public bool IsReady { get; private set; }

    public void Replace(VocabularySnapshot snapshot)
    {
        _snapshot = snapshot;
        IsReady = true;
    }
}
