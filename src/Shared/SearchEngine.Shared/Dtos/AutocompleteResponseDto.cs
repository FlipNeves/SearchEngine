namespace SearchEngine.Shared.Dtos;

public sealed record AutocompleteResponseDto(
    string Prefix,
    IReadOnlyList<AutocompleteSuggestion> Suggestions);

public sealed record AutocompleteSuggestion(string Text, bool IsCorrection);
