using SearchEngine.FromScratch.Infrastructure.DataModels;

namespace SearchEngine.FromScratch.Infrastructure.Daos;

public interface IIndexStatsDao
{
    Task<IndexStatsDataModel> GetAsync(CancellationToken ct = default);
    Task RecomputeAsync(CancellationToken ct = default);
}
