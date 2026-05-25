namespace SearchEngine.Core.Indexing;

public sealed class Trie
{
    private readonly TrieNode _root = new();
    private readonly int _topN;
    private readonly Dictionary<string, int> _frequencies = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public Trie(int topN = 10) => _topN = topN;

    public TrieNode Root => _root;

    public void Insert(string word, int frequencyDelta = 1)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        var normalized = word.Trim().ToLowerInvariant();

        lock (_gate)
        {
            _frequencies.TryGetValue(normalized, out var current);
            var freq = current + frequencyDelta;
            _frequencies[normalized] = freq;

            var node = _root;
            foreach (var ch in normalized)
            {
                if (!node.Children.TryGetValue(ch, out var child))
                {
                    child = new TrieNode();
                    node.Children[ch] = child;
                }
                node = child;
                UpdateTopSuggestions(node, normalized, freq);
            }

            node.IsTerminal = true;
            node.Frequency = freq;
        }
    }

    public IReadOnlyList<string> Autocomplete(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return Array.Empty<string>();
        var normalized = prefix.Trim().ToLowerInvariant();

        lock (_gate)
        {
            var node = _root;
            foreach (var ch in normalized)
            {
                if (!node.Children.TryGetValue(ch, out var next))
                    return Array.Empty<string>();
                node = next;
            }
            return node.TopSuggestions.ToArray();
        }
    }

    private void UpdateTopSuggestions(TrieNode node, string word, int frequency)
    {
        node.TopSuggestions.RemoveAll(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));

        var insertAt = node.TopSuggestions.FindIndex(w => _frequencies[w] < frequency);
        if (insertAt < 0) node.TopSuggestions.Add(word);
        else node.TopSuggestions.Insert(insertAt, word);

        if (node.TopSuggestions.Count > _topN)
            node.TopSuggestions.RemoveAt(node.TopSuggestions.Count - 1);
    }
}
