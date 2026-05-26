using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Polly;
using Polly.Extensions.Http;
using SearchEngine.Core.Abstractions;
using SearchEngine.Crawler.Options;
using SearchEngine.Crawler.Workers;
using SearchEngine.Infrastructure.Mongo;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection("Crawler"));
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(opts.ConnectionString);
});

builder.Services.AddSingleton<ISearchService, MongoSearchService>();

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
