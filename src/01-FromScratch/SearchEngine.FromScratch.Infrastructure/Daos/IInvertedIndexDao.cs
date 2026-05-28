using SearchEngine.FromScratch.Infrastructure.DataModels;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public interface IInvertedIndexDao
{
    Task UpsertPostingsAsync(IReadOnlyCollection<(string Term, PostingDataModel Posting)> postings, CancellationToken ct = default);

    Task<IReadOnlyList<InvertedIndexDataModel>> FindByTermsAsync(IReadOnlyCollection<string> terms, CancellationToken ct = default);

    IAsyncEnumerable<InvertedIndexDataModel> StreamAllAsync(CancellationToken ct = default);
}
