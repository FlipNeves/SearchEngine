using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SearchEngine.Crawler.Options;
using SearchEngine.Crawler.Workers;
using SearchEngine.FromScratch.Core.Text;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.FromScratch.Infrastructure.Indexing;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection("Crawler"));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));

builder.Services.AddSharedMongo();
builder.Services.AddScoped<IInvertedIndexDao, InvertedIndexDao>();
builder.Services.AddScoped<IPhraseIndexDao, PhraseIndexDao>();
builder.Services.AddScoped<IIndexStatsDao, IndexStatsDao>();
builder.Services.AddScoped<IPageIndexer, FromScratchIndexer>();
builder.Services.AddSingleton(LanguageDetector.Default());

builder.Services.AddHttpClient("crawler", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<CrawlerOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
})
.AddPolicyHandler(GetRetryPolicy());

builder.Services.AddHostedService<WebCrawlerWorker>();

await builder.Build().RunAsync();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt =>
                TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200)
                + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)));
