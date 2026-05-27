using MongoDB.Bson;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;

namespace SearchEngine.FromScratch.Infrastructure.Indexing;

public sealed class FromScratchIndexer : IPageIndexer
{
    private readonly IInvertedIndexDao _index;

    public FromScratchIndexer(IInvertedIndexDao index) => _index = index;

    public Task IndexAsync(WebPage page, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(page.Id, out var pageOid)) return Task.CompletedTask;

        var tokens = Tokenizer
            .Tokenize($"{page.Title} {page.Content}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return _index.UpsertPostingsAsync(tokens, pageOid, ct);
    }
}
