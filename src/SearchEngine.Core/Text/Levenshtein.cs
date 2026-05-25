namespace SearchEngine.Core.Text;

public static class Levenshtein
{
    public static int Distance(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        if (source.Length < target.Length)
        {
            var swap = source;
            source = target;
            target = swap;
        }

        var width = target.Length + 1;
        Span<int> prev = width <= 256 ? stackalloc int[width] : new int[width];
        Span<int> curr = width <= 256 ? stackalloc int[width] : new int[width];

        for (var j = 0; j < width; j++) prev[j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            curr[0] = i;
            var sourceChar = char.ToLowerInvariant(source[i - 1]);

            for (var j = 1; j < width; j++)
            {
                var cost = sourceChar == char.ToLowerInvariant(target[j - 1]) ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            curr.CopyTo(prev);
        }

        return prev[target.Length];
    }

    public static int Distance(string source, string target)
        => Distance(source.AsSpan(), target.AsSpan());
}
