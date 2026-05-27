using System.Reflection;
using MongoDB.Driver;
using SearchEngine.Shared.Persistence.Attributes;

namespace SearchEngine.Shared.Persistence.Internal;

public static class CollectionResolver
{
    public static IMongoCollection<TDocument> Resolve<TDocument>(IMongoDatabase database)
    {
        var attr = typeof(TDocument).GetCustomAttribute<CollectionNameAttribute>()
            ?? throw new InvalidOperationException(
                $"Type {typeof(TDocument).Name} is missing [CollectionName]. Annotate the DataModel before resolving its collection.");

        return database.GetCollection<TDocument>(attr.Name);
    }
}
