namespace SearchEngine.FromScratch.Core.Indexing;

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

    public IReadOnlyCollection<string> AllWords()
    {
        lock (_gate) { return _frequencies.Keys.ToArray(); }
    }

    public int Frequency(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return 0;
        var normalized = word.Trim().ToLowerInvariant();
        lock (_gate) { return _frequencies.TryGetValue(normalized, out var freq) ? freq : 0; }
    }

    public IReadOnlyList<(string Word, int Distance)> FuzzyAutocomplete(string prefix, int maxEdits)
    {
        if (string.IsNullOrEmpty(prefix)) return Array.Empty<(string, int)>();
        var normalized = prefix.Trim().ToLowerInvariant();
        if (normalized.Length == 0) return Array.Empty<(string, int)>();

        lock (_gate)
        {
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            var initialRow = new int[normalized.Length + 1];
            for (var j = 0; j <= normalized.Length; j++) initialRow[j] = j;

            foreach (var (ch, child) in _root.Children)
            {
                if (ch != normalized[0]) continue;
                Walk(child, ch, initialRow, null, '\0', normalized, maxEdits, best);
            }

            return best
                .OrderBy(kv => kv.Value)
                .ThenByDescending(kv => _frequencies.GetValueOrDefault(kv.Key))
                .ThenBy(kv => kv.Key.Length)
                .Select(kv => (kv.Key, kv.Value))
                .ToArray();
        }
    }

    private static void Walk(
        TrieNode node, char ch, int[] prevRow, int[]? prevPrevRow, char prevCh,
        string prefix, int maxEdits, Dictionary<string, int> best)
    {
        var m = prefix.Length;
        var row = new int[m + 1];
        row[0] = prevRow[0] + 1;
        var min = row[0];

        for (var j = 1; j <= m; j++)
        {
            var cost = prefix[j - 1] == ch ? 0 : 1;
            var value = Math.Min(Math.Min(row[j - 1] + 1, prevRow[j] + 1), prevRow[j - 1] + cost);
            if (j > 1 && prevPrevRow is not null && prefix[j - 1] == prevCh && prefix[j - 2] == ch)
                value = Math.Min(value, prevPrevRow[j - 2] + 1);
            row[j] = value;
            if (value < min) min = value;
        }

        if (row[m] <= maxEdits)
            foreach (var word in node.TopSuggestions)
                if (!best.TryGetValue(word, out var existing) || row[m] < existing)
                    best[word] = row[m];

        if (min > maxEdits) return;

        foreach (var (nextCh, child) in node.Children)
            Walk(child, nextCh, row, prevRow, ch, prefix, maxEdits, best);
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
