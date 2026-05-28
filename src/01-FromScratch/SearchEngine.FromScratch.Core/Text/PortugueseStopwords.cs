namespace SearchEngine.FromScratch.Core.Text;

public static class PortugueseStopwords
{
    private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
    {
        "o", "a", "os", "as", "um", "uma", "uns", "umas",
        "de", "do", "da", "dos", "das",
        "em", "no", "na", "nos", "nas",
        "ao", "aos", "por", "para", "com",
        "e"
    };

    public static bool Contains(string token) => Words.Contains(token);
}
