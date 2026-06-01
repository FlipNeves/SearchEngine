namespace SearchEngine.Shared.Dtos;

public sealed record SearchResponseDto(
    string Query,
    IReadOnlyList<SearchHit> Results,
    DidYouMean? DidYouMean);

public sealed record SearchHit(string Url, string Title, string Preview, double Score);

public sealed record DidYouMean(string Original, string Corrected);
