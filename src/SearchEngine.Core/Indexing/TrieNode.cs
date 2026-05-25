namespace SearchEngine.Core.Indexing;

public sealed class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; } = new();
    public bool IsTerminal { get; internal set; }
    public int Frequency { get; internal set; }
    public List<string> TopSuggestions { get; } = new();
}
