using System.Globalization;
using System.Text;

namespace SearchEngine.FromScratch.Core.Text;

public static class Tokenizer
{
    public static IEnumerable<string> Tokenize(string text, bool removeStopwords = true)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var normalized = text.Normalize(NormalizationForm.FormD);

        var buffer = new StringBuilder();
        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);

            if (category is UnicodeCategory.NonSpacingMark)
                continue;

            var isWord = category is UnicodeCategory.LowercaseLetter
                                  or UnicodeCategory.UppercaseLetter
                                  or UnicodeCategory.OtherLetter
                                  or UnicodeCategory.DecimalDigitNumber;

            if (isWord)
            {
                foreach (var ch in rune.ToString())
                    buffer.Append(char.ToLowerInvariant(ch));
            }
            else if (buffer.Length > 0)
            {
                var token = buffer.ToString();
                buffer.Clear();
                if (token.Length >= 2 && (!removeStopwords || !PortugueseStopwords.Contains(token)))
                    yield return token;
            }
        }

        if (buffer.Length >= 2)
        {
            var token = buffer.ToString();
            if (!removeStopwords || !PortugueseStopwords.Contains(token))
                yield return token;
        }
    }
}
