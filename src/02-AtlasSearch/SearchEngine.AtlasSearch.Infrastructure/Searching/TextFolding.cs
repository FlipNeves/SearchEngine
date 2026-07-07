using System.Globalization;
using System.Text;

namespace SearchEngine.AtlasSearch.Infrastructure.Searching;

internal static class TextFolding
{
    private static readonly char[] NonWordChars =
        Enumerable.Range(0, 128).Select(c => (char)c).Where(c => !char.IsLetterOrDigit(c)).ToArray();

    public static string Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IEnumerable<string> SplitWords(string text)
        => text.Split(NonWordChars, StringSplitOptions.RemoveEmptyEntries);
}
