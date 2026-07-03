using MongoDB.Bson;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.FromScratch.Infrastructure.DataModels;
using SearchEngine.Shared.Domain.Entities;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Text;

namespace SearchEngine.FromScratch.Infrastructure.Indexing;

public sealed class FromScratchIndexer : IPageIndexer
{
    private static readonly int[] PhraseSizes = { 2, 3 };

    private readonly IInvertedIndexDao _index;
    private readonly IPhraseIndexDao _phrase;
    private readonly IPagesRepository _pages;
    private readonly IIndexStatsDao _stats;
    private readonly LanguageDetector _language;

    public FromScratchIndexer(
        IInvertedIndexDao index,
        IPhraseIndexDao phrase,
        IPagesRepository pages,
        IIndexStatsDao stats,
        LanguageDetector language)
    {
        _index = index;
        _phrase = phrase;
        _pages = pages;
        _stats = stats;
        _language = language;
    }

    public async Task IndexAsync(WebPage page, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(page.Id, out var pageOid)) return;

        var titleTokens = Tokenizer.Tokenize(page.Title).ToArray();
        var contentTokens = Tokenizer.Tokenize(page.Content).ToArray();

        var byTerm = new Dictionary<string, PostingDataModel>(StringComparer.Ordinal);

        foreach (var token in titleTokens)
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfTitle++;
            posting.PositionsTitle.Add(new PositionDataModel { Start = token.Start, End = token.End });
        }

        foreach (var token in contentTokens)
        {
            var posting = GetOrCreate(byTerm, token.Value, pageOid);
            posting.TfContent++;
            posting.PositionsContent.Add(new PositionDataModel { Start = token.Start, End = token.End });
        }

        var phraseByKey = new Dictionary<(string Phrase, int Size), PostingDataModel>();

        foreach (var size in PhraseSizes)
        {
            foreach (var (phrase, start, end) in NGram.Generate(titleTokens, size))
            {
                var posting = GetOrCreatePhrase(phraseByKey, phrase, size, pageOid);
                posting.TfTitle++;
                posting.PositionsTitle.Add(new PositionDataModel { Start = start, End = end });
            }

            foreach (var (phrase, start, end) in NGram.Generate(contentTokens, size))
            {
                var posting = GetOrCreatePhrase(phraseByKey, phrase, size, pageOid);
                posting.TfContent++;
                posting.PositionsContent.Add(new PositionDataModel { Start = start, End = end });
            }
        }

        var termOps = byTerm.Select(kv => (kv.Key, kv.Value)).ToArray();
        var phraseOps = phraseByKey.Select(kv => (kv.Key.Phrase, kv.Key.Size, kv.Value)).ToArray();

        if (termOps.Length > 0)
            await _index.UpsertPostingsAsync(termOps, ct);

        if (phraseOps.Length > 0)
            await _phrase.UpsertPostingsAsync(phraseOps, ct);

        if (termOps.Length > 0 || phraseOps.Length > 0)
        {
            var language = _language.Detect($"{page.Title} {page.Content}").Language;
            await _pages.UpdateDerivedFieldsAsync(page.Id, titleTokens.Length, contentTokens.Length, language, ct);
            await _stats.RecomputeAsync(ct);
        }
    }

    private static PostingDataModel GetOrCreate(Dictionary<string, PostingDataModel> byTerm, string term, ObjectId docId)
    {
        if (!byTerm.TryGetValue(term, out var posting))
        {
            posting = new PostingDataModel { DocId = docId };
            byTerm[term] = posting;
        }
        return posting;
    }

    private static PostingDataModel GetOrCreatePhrase(
        Dictionary<(string Phrase, int Size), PostingDataModel> byKey,
        string phrase, int size, ObjectId docId)
    {
        var key = (phrase, size);
        if (!byKey.TryGetValue(key, out var posting))
        {
            posting = new PostingDataModel { DocId = docId };
            byKey[key] = posting;
        }
        return posting;
    }
}
