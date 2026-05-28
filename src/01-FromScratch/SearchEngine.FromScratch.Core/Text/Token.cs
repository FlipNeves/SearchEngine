namespace SearchEngine.FromScratch.Core.Text;

public readonly record struct Token(string Value, int Start, int End);
