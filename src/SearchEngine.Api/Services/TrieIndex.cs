using SearchEngine.Core.Indexing;

namespace SearchEngine.Api.Services;

public sealed class TrieIndex
{
    private Trie _trie = new(topN: 10);

    public Trie Current => _trie;
    public bool IsReady { get; private set; }

    public void Replace(Trie newTrie)
    {
        _trie = newTrie;
        IsReady = true;
    }
}
