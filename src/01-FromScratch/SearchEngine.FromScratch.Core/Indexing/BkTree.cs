namespace SearchEngine.FromScratch.Core.Indexing;

// Burkhard-Keller tree: a metric tree that answers "which indexed words are within edit
// distance r of this query?" in roughly O(log N) instead of a linear scan. Children of a
// node are bucketed by their exact distance to that node; on search we only descend buckets
// k where |k - d| <= r (triangle inequality). Requires a true metric distance function.
public sealed class BkTree
{
    private readonly Func<string, string, int> _distance;
    private Node? _root;

    public BkTree(Func<string, string, int> distance) => _distance = distance;

    public int Count { get; private set; }

    public void Add(string word)
    {
        if (string.IsNullOrEmpty(word)) return;

        if (_root is null)
        {
            _root = new Node(word);
            Count = 1;
            return;
        }

        var node = _root;
        while (true)
        {
            var d = _distance(word, node.Word);
            if (d == 0) return; // already present

            if (!node.Children.TryGetValue(d, out var child))
            {
                node.Children[d] = new Node(word);
                Count++;
                return;
            }

            node = child;
        }
    }

    // Every indexed word within maxDistance of query, paired with that distance.
    public List<(string Word, int Distance)> Search(string query, int maxDistance)
    {
        var matches = new List<(string, int)>();
        if (_root is not null) Visit(_root, query, maxDistance, matches);
        return matches;
    }

    private void Visit(Node node, string query, int maxDistance, List<(string, int)> matches)
    {
        var d = _distance(query, node.Word);
        if (d <= maxDistance) matches.Add((node.Word, d));

        var lo = d - maxDistance;
        var hi = d + maxDistance;
        foreach (var (k, child) in node.Children)
            if (k >= lo && k <= hi)
                Visit(child, query, maxDistance, matches);
    }

    private sealed class Node
    {
        public Node(string word) => Word = word;
        public string Word { get; }
        public Dictionary<int, Node> Children { get; } = new();
    }
}
