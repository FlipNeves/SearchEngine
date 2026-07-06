using SearchEngine.AtlasSearch.Infrastructure.Searching;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Dtos;

namespace SearchEngine.AtlasSearch.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/search", SearchAsync)
            .WithName("Search")
            .WithTags("Search")
            .Produces<SearchResponseDto>()
            .WithDescription("Busca via $search do Atlas (Lucene). Fuzzy matching embutido cobre erros de digitação e o motor sintetiza `didYouMean` comparando os termos da query com as palavras destacadas nos highlights. Passe `correct=false` para desligar o fuzzy e a correção.");

        app.MapGet("/autocomplete", AutocompleteAsync)
            .WithName("Autocomplete")
            .WithTags("Search")
            .Produces<AutocompleteResponseDto>()
            .WithDescription("Search-as-you-type via operador `autocomplete` do Atlas sobre o título das páginas. Sugere títulos de documentos, não palavras do vocabulário — `isCorrection` é sempre false neste motor.");
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

    private static async Task<IResult> AutocompleteAsync(
        string prefix,
        AtlasAutocomplete autocomplete,
        CancellationToken ct,
        int top = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Results.Problem("query parameter 'prefix' is required", statusCode: 400);

        var response = await autocomplete.SuggestAsync(prefix.Trim(), top, ct);
        return Results.Ok(response);
    }
}
