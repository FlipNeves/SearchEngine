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
            .WithDescription("Busca via $search do Atlas (Lucene). Fuzzy matching embutido cobre erros de digitação — `didYouMean` é sempre null neste motor. Passe `correct=false` para desligar o fuzzy.");
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
}
