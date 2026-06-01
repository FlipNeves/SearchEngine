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
            .WithSummary("Sugere completions para um prefixo (Trie em memória).")
            .Produces<IEnumerable<string>>(StatusCodes.Status200OK)
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

    private static IResult Autocomplete(string prefix, TrieIndex trieIndex, int top = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Results.Problem("query parameter 'prefix' is required", statusCode: 400);

        if (!trieIndex.IsReady)
            return Results.Problem("index is still loading, try again in a few seconds", statusCode: 503);

        var suggestions = trieIndex.Current.Autocomplete(prefix).Take(top);
        return Results.Ok(suggestions);
    }
}
