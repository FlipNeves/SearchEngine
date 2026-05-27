using MongoDB.Bson;
using SearchEngine.FromScratch.Infrastructure.DataModels;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public interface IInvertedIndexDao
{
    Task UpsertPostingsAsync(IReadOnlyCollection<string> words, ObjectId pageId, CancellationToken ct = default);

    Task<IReadOnlyList<InvertedIndexDataModel>> FindByWordsAsync(IReadOnlyCollection<string> words, CancellationToken ct = default);

    IAsyncEnumerable<InvertedIndexDataModel> StreamAllAsync(CancellationToken ct = default);
}
