namespace SearchEngine.Shared.Dtos;

public sealed record SuggestionResultDto(string Word, int Distance, int Score);
