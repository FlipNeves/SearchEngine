using SearchEngine.FromScratch.Core.Indexing;
using SearchEngine.Shared.Text;

namespace SearchEngine.FromScratch.Api.Services;

// Immutable view of the in-memory vocabulary built from one refresh: the prefix Trie,
// the BK-tree (fuzzy / spell-correction) and the page titles (autocomplete). All reflect
// the SAME snapshot, so a reader can never see a new Trie paired with a stale BK-tree.
public sealed class VocabularySnapshot
{
    public required Trie Trie { get; init; }
    public required BkTree BkTree { get; init; }
    public required IReadOnlyList<TitleEntry> Titles { get; init; }

    public static VocabularySnapshot Empty { get; } = new()
    {
        Trie = new Trie(),
        BkTree = new BkTree(DamerauLevenshtein.Distance),
        Titles = Array.Empty<TitleEntry>(),
    };
}

public sealed record TitleEntry(string Title, string Folded)
{
    public static TitleEntry From(string title) => new(title, TextFolding.Fold(title));
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
