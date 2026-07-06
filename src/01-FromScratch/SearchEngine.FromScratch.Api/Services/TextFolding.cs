using System.Globalization;
using System.Text;

namespace SearchEngine.FromScratch.Api.Services;

public static class TextFolding
{
    public static string Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
