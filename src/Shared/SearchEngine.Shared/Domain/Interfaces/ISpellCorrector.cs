namespace SearchEngine.Shared.Domain.Interfaces;

public interface ISpellCorrector
{
    bool IsReady { get; }

    string? Correct(string term);
}
