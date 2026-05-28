using System.Globalization;
using System.Text;

namespace SearchEngine.FromScratch.Core.Text;

public static class Tokenizer
{
    public static IEnumerable<Token> Tokenize(string text, bool removeStopwords = true)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var buffer = new StringBuilder();
        var tokenStart = -1;
        var charIndex = 0;

        foreach (var origRune in text.EnumerateRunes())
        {
            var runeLen = origRune.Utf16SequenceLength;
            var decomposed = origRune.ToString().Normalize(NormalizationForm.FormD);

            var addedWordChar = false;
            foreach (var dRune in decomposed.EnumerateRunes())
            {
                var category = Rune.GetUnicodeCategory(dRune);
                if (category is UnicodeCategory.NonSpacingMark)
                    continue;

                var isWord = category is UnicodeCategory.LowercaseLetter
                                      or UnicodeCategory.UppercaseLetter
                                      or UnicodeCategory.OtherLetter
                                      or UnicodeCategory.DecimalDigitNumber;

                if (!isWord) continue;

                if (tokenStart < 0) tokenStart = charIndex;
                foreach (var ch in dRune.ToString())
                    buffer.Append(char.ToLowerInvariant(ch));
                addedWordChar = true;
            }

            if (!addedWordChar && buffer.Length > 0)
            {
                var value = buffer.ToString();
                var start = tokenStart;
                var end = charIndex;
                buffer.Clear();
                tokenStart = -1;
                if (value.Length >= 2 && (!removeStopwords || !PortugueseStopwords.Contains(value)))
                    yield return new Token(value, start, end);
            }

            charIndex += runeLen;
        }

        if (buffer.Length > 0)
        {
            var value = buffer.ToString();
            if (value.Length >= 2 && (!removeStopwords || !PortugueseStopwords.Contains(value)))
                yield return new Token(value, tokenStart, charIndex);
        }
    }
}
