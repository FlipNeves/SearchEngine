using System.Globalization;
using System.Text;

namespace SearchEngine.FromScratch.Core.Text;

public static class Tokenizer
{
    public static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var buffer = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
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
                if (buffer.Length >= 2) yield return buffer.ToString();
                buffer.Clear();
            }
        }

        if (buffer.Length >= 2) yield return buffer.ToString();
    }
}
