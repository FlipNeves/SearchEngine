using SearchEngine.FromScratch.Api.Services;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.FromScratch.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", SearchAsync)
            .WithName("Search")
            .WithTags("Search")
            .WithSummary("Busca páginas e auto-corrige termos fora do vocabulário.")
            .WithDescription("Tokeniza `q`, ranqueia por BM25 + phrase boost. Se algum token não existe no índice e a correção tem resultados, busca a correção e retorna `didYouMean`. Passe `correct=false` para desligar a auto-correção (usado pelo link \"buscar pelo original\").")
            .Produces<SearchResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/autocomplete", Autocomplete)
            .WithName("Autocomplete")
            .WithTags("Search")
            .WithSummary("Search-as-you-type sobre os títulos das páginas. Sugere títulos de documentos, não palavras do vocabulário — `isCorrection` é sempre false neste motor.")
            .Produces<AutocompleteResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q,
        ISearchEngine searchEngine,
        CancellationToken ct,
        int top = 20,
        bool correct = true)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("query parameter 'q' is required", statusCode: 400);

        var response = await searchEngine.SearchAsync(q, top, correct, ct);
        return Results.Ok(response);
    }

    private static IResult Autocomplete(
        string prefix,
        VocabularyIndex vocabulary,
        int top = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Results.Problem("query parameter 'prefix' is required", statusCode: 400);

        if (!vocabulary.IsReady)
            return Results.Problem("index is still loading, try again in a few seconds", statusCode: 503);

        var suggestions = BuildSuggestions(vocabulary.Current, prefix.Trim(), top);

        return Results.Ok(new AutocompleteResponseDto(prefix, suggestions));
    }

    private static IReadOnlyList<AutocompleteSuggestion> BuildSuggestions(
        VocabularySnapshot snapshot, string prefix, int top)
    {
        var folded = TextFolding.Fold(prefix);
        if (folded.Length == 0) return Array.Empty<AutocompleteSuggestion>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return snapshot.Titles
            .Select(t => (t.Title, Rank: MatchRank(t.Folded, folded)))
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Title.Length)
            .Where(x => seen.Add(x.Title))
            .Take(top)
            .Select(x => new AutocompleteSuggestion(x.Title, IsCorrection: false))
            .ToArray();
    }

    private static int MatchRank(string foldedTitle, string foldedPrefix)
    {
        if (foldedTitle.StartsWith(foldedPrefix, StringComparison.Ordinal)) return 0;

        var index = foldedTitle.IndexOf(foldedPrefix, StringComparison.Ordinal);
        while (index > 0)
        {
            if (!char.IsLetterOrDigit(foldedTitle[index - 1])) return 1;
            index = foldedTitle.IndexOf(foldedPrefix, index + 1, StringComparison.Ordinal);
        }

        return -1;
    }
}
