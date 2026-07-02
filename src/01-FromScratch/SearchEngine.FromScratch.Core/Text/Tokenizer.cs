using System.Globalization;
using System.Text;

namespace SearchEngine.FromScratch.Core.Text;

public static class Tokenizer
{
    public static IEnumerable<Token> Tokenize(string text, bool removeStopwords = true)
    {
        var tokens = new List<Token>();
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        var buffer = new StringBuilder();
        var pendingSymbols = new StringBuilder();
        var tokenStart = -1;
        var pendingDotStart = -1;
        var charIndex = 0;

        void Emit(int end)
        {
            if (buffer.Length == 0)
            {
                pendingSymbols.Clear();
                tokenStart = -1;
                return;
            }

            if (pendingSymbols.Length > 0)
            {
                buffer.Append(pendingSymbols);
                pendingSymbols.Clear();
            }

            var value = buffer.ToString();
            var start = tokenStart;
            buffer.Clear();
            tokenStart = -1;

            if (value.Length >= 2 && (!removeStopwords || !PortugueseStopwords.Contains(value)))
                tokens.Add(new Token(value, start, end));
        }

        foreach (var origRune in text.EnumerateRunes())
        {
            var runeLen = origRune.Utf16SequenceLength;
            var wordChars = ExtractWordChars(origRune);

            if (wordChars.Length > 0)
            {
                if (pendingSymbols.Length > 0)
                {
                    var wordEnd = charIndex - pendingSymbols.Length;
                    pendingSymbols.Clear();
                    Emit(wordEnd);
                }

                if (pendingDotStart >= 0)
                {
                    tokenStart = pendingDotStart;
                    buffer.Append('.');
                    pendingDotStart = -1;
                }

                if (tokenStart < 0) tokenStart = charIndex;
                buffer.Append(wordChars);
            }
            else if (origRune.Value == '.')
            {
                if (buffer.Length > 0)
                    Emit(charIndex);
                else if (pendingDotStart < 0)
                    pendingDotStart = charIndex;
            }
            else if (origRune.Value is '#' or '+')
            {
                if (buffer.Length > 0)
                    pendingSymbols.Append((char)origRune.Value);
                else
                    pendingDotStart = -1;
            }
            else
            {
                Emit(charIndex);
                pendingDotStart = -1;
            }

            charIndex += runeLen;
        }

        Emit(charIndex);
        return tokens;
    }

    private static string ExtractWordChars(Rune origRune)
    {
        var decomposed = origRune.ToString().Normalize(NormalizationForm.FormD);

        StringBuilder? sb = null;
        foreach (var dRune in decomposed.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(dRune);
            if (category is UnicodeCategory.NonSpacingMark)
                continue;

            var isWord = category is UnicodeCategory.LowercaseLetter
                                  or UnicodeCategory.UppercaseLetter
                                  or UnicodeCategory.OtherLetter
                                  or UnicodeCategory.DecimalDigitNumber;
            if (!isWord)
                continue;

            sb ??= new StringBuilder();
            foreach (var ch in dRune.ToString())
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb?.ToString() ?? string.Empty;
    }
}
