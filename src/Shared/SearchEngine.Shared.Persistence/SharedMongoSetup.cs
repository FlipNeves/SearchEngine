using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence.Repositories;

namespace SearchEngine.Shared.Persistence;

public static class SharedMongoSetup
{
    private static int _conventionsRegistered;

    public static IServiceCollection AddSharedMongo(this IServiceCollection services)
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 0)
        {
            var pack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("camelCase", pack, _ => true);
        }

        services.AddSingleton<IMongoClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
                throw new InvalidOperationException(
                    "Mongo:ConnectionString is empty. Set via user-secrets or appsettings.");
            return new MongoClient(opts.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(opts.Database);
        });

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IPagesRepository, PagesRepository>();

        return services;
    }
}
