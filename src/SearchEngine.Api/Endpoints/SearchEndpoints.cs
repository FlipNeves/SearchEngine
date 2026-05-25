using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SearchEngine.Api.Options;
using SearchEngine.Api.Services;
using SearchEngine.Core.Abstractions;
using SearchEngine.Core.Text;

namespace SearchEngine.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", SearchAsync)
            .WithName("Search")
            .WithTags("Search")
            .WithSummary("Busca páginas que contêm qualquer um dos tokens da query.")
            .WithDescription("Tokeniza `q`, consulta o índice invertido com `$in` no Mongo e retorna as páginas correspondentes (semântica OR entre tokens).")
            .WithOpenApi(op =>
            {
                op.Parameters[0].Description = "Texto a pesquisar. Será tokenizado (lower-invariant, ≥2 chars).";
                op.Parameters[1].Description = "Limite de resultados (default 20).";
                return op;
            })
            .Produces<IEnumerable<SearchResultDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/autocomplete", Autocomplete)
            .WithName("Autocomplete")
            .WithTags("Search")
            .WithSummary("Sugere completions para um prefixo (Trie em memória).")
            .WithDescription("Navega o Trie pelo prefixo e retorna a lista pré-computada de top palavras daquele nó. O(|prefix|).")
            .WithOpenApi(op =>
            {
                op.Parameters[0].Description = "Prefixo a completar.";
                op.Parameters[1].Description = "Limite de sugestões (default 10).";
                return op;
            })
            .Produces<IEnumerable<string>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/suggest", Suggest)
            .WithName("Suggest")
            .WithTags("Search")
            .WithSummary("Sugere correções de digitação via Levenshtein.")
            .WithDescription("Calcula distância de Levenshtein entre `q` e cada palavra do índice; retorna as mais próximas (distância > 0 e ≤ MaxSuggestionDistance).")
            .WithOpenApi(op =>
            {
                op.Parameters[0].Description = "Termo possivelmente digitado errado.";
                op.Parameters[1].Description = "Limite de sugestões (default 10).";
                return op;
            })
            .Produces<IEnumerable<SuggestionResultDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q,
        ISearchService searchService,
        CancellationToken ct,
        int top = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("query parameter 'q' is required", statusCode: 400);

        var pages = await searchService.ExecuteSearchAsync(q, ct);
        var results = pages
            .Take(top)
            .Select(p => new SearchResultDto(p.Url, p.Title, Truncate(p.Content, 200)));
        return Results.Ok(results);
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
            .Select(w => new SuggestionCandidate(w, Levenshtein.Distance(w, query)))
            .Where(c => c.Distance > 0 && c.Distance <= maxDistance)
            .OrderBy(c => c.Distance)
            .ThenBy(c => c.Word, StringComparer.Ordinal)
            .Take(top)
            .Select(c => new SuggestionResultDto(c.Word, c.Distance));

        return Results.Ok(corrections);
    }

    private static string Truncate(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    private readonly record struct SuggestionCandidate(string Word, int Distance);
}

public sealed record SearchResultDto(string Url, string Title, string Preview);
public sealed record SuggestionResultDto(string Word, int Distance);
