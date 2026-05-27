using Microsoft.Extensions.Options;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Api.Services;
using SearchEngine.FromScratch.Core.Text;
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
            .WithSummary("Busca páginas que contêm qualquer um dos tokens da query.")
            .WithDescription("Tokeniza `q`, consulta o índice invertido com `$in` no Mongo e retorna as páginas correspondentes (semântica OR entre tokens).")
            .Produces<SearchResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/autocomplete", Autocomplete)
            .WithName("Autocomplete")
            .WithTags("Search")
            .WithSummary("Sugere completions para um prefixo (Trie em memória).")
            .Produces<IEnumerable<string>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/suggest", Suggest)
            .WithName("Suggest")
            .WithTags("Search")
            .WithSummary("Sugere correções de digitação via Levenshtein.")
            .Produces<IEnumerable<SuggestionResultDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q,
        ISearchEngine searchEngine,
        CancellationToken ct,
        int top = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("query parameter 'q' is required", statusCode: 400);

        var response = await searchEngine.SearchAsync(q, top, ct);
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

    private static IResult Suggest(
        string q,
        TrieIndex trieIndex,
        IOptions<TrieRefreshOptions> options,
        int top = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("query parameter 'q' is required", statusCode: 400);

        if (!trieIndex.IsReady)
            return Results.Problem("index is still loading, try again in a few seconds", statusCode: 503);

        var maxDistance = options.Value.MaxSuggestionDistance;
        var query = q.Trim().ToLowerInvariant();

        var corrections = trieIndex.Current.AllWords()
            .Where(w => Math.Abs(w.Length - query.Length) <= maxDistance)
            .Select(w =>
            {
                var distance = Levenshtein.Distance(w, query);
                var prefix = CommonPrefixLength(w, query);
                var score = distance * 10 - prefix * 2;
                return new SuggestionResultDto(w, distance, score);
            })
            .Where(x => x.Distance > 0 && x.Distance <= maxDistance)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Distance)
            .Take(top);

        return Results.Ok(corrections);
    }

    private static int CommonPrefixLength(string a, string b)
    {
        var length = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < length && a[i] == b[i]) i++;
        return i;
    }
}

public sealed record SuggestionResultDto(string Word, int Distance, int Score);
