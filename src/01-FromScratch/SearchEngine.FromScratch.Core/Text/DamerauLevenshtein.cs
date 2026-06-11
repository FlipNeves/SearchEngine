namespace SearchEngine.FromScratch.Core.Text;

// True Damerau-Levenshtein distance: insertion, deletion, substitution and ADJACENT
// transposition, each cost 1. Unlike the OSA (Optimal String Alignment) variant, this
// one satisfies the triangle inequality, which the BK-tree relies on to prune soundly.
public static class DamerauLevenshtein
{
    public static int Distance(string source, string target)
    {
        source ??= string.Empty;
        target ??= string.Empty;

        var n = source.Length;
        var m = target.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var maxDist = n + m;

        // Matrix carries two sentinel rows/cols: real cells start at [1,1]; row/col 0 hold
        // the "infinity" used by the transposition rule. So array index = logical index + 1.
        var d = new int[n + 2, m + 2];
        d[0, 0] = maxDist;
        for (var i = 0; i <= n; i++) { d[i + 1, 0] = maxDist; d[i + 1, 1] = i; }
        for (var j = 0; j <= m; j++) { d[0, j + 1] = maxDist; d[1, j + 1] = j; }

        // For each char, the last row in which it appeared (1-based; 0 = never seen).
        var lastRow = new Dictionary<char, int>();

        for (var i = 1; i <= n; i++)
        {
            var lastMatchCol = 0; // last col j in this row where source[i] == target[j]
            var sc = char.ToLowerInvariant(source[i - 1]);

            for (var j = 1; j <= m; j++)
            {
                var tc = char.ToLowerInvariant(target[j - 1]);
                var lastMatchRow = lastRow.TryGetValue(tc, out var r) ? r : 0;
                var l = lastMatchCol; // read BEFORE updating, per the algorithm

                var cost = sc == tc ? 0 : 1;
                if (cost == 0) lastMatchCol = j;

                d[i + 1, j + 1] = Min4(
                    d[i, j] + cost,        // substitution
                    d[i + 1, j] + 1,       // insertion
                    d[i, j + 1] + 1,       // deletion
                    d[lastMatchRow, l] + (i - lastMatchRow - 1) + 1 + (j - l - 1)); // transposition
            }

            lastRow[sc] = i;
        }

        return d[n + 1, m + 1];
    }

    private static int Min4(int a, int b, int c, int d)
        => Math.Min(Math.Min(a, b), Math.Min(c, d));
}
