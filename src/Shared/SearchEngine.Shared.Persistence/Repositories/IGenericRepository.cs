namespace SearchEngine.Shared.Persistence.Repositories;

public interface IGenericRepository<TDocument> where TDocument : class
{
    Task AddAsync(TDocument document, CancellationToken ct = default);
    Task<TDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<TDocument>> ListByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default);
}
