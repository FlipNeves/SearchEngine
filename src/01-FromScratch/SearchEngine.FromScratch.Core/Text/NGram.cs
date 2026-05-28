namespace SearchEngine.FromScratch.Core.Text;

public static class NGram
{
    public static IEnumerable<(string Phrase, int Start, int End)> Generate(
        IReadOnlyList<Token> tokens, int size)
    {
        if (size < 2 || tokens.Count < size) yield break;

        var parts = new string[size];
        for (var i = 0; i <= tokens.Count - size; i++)
        {
            for (var k = 0; k < size; k++)
                parts[k] = tokens[i + k].Value;

            var phrase = string.Join('_', parts);
            yield return (phrase, tokens[i].Start, tokens[i + size - 1].End);
        }
    }
}
