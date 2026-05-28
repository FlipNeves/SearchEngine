using SearchEngine.FromScratch.Infrastructure.DataModels;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public interface IPhraseIndexDao
{
    Task UpsertPostingsAsync(
        IReadOnlyCollection<(string Phrase, int Size, PostingDataModel Posting)> postings,
        CancellationToken ct = default);

    Task<IReadOnlyList<PhraseIndexDataModel>> FindByPhrasesAsync(
        IReadOnlyCollection<string> phrases,
        CancellationToken ct = default);
}
