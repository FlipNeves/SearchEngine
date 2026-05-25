using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using SearchEngine.Api.Endpoints;
using SearchEngine.Api.Options;
using SearchEngine.Api.Services;
using SearchEngine.Core.Abstractions;
using SearchEngine.Infrastructure.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<TrieRefreshOptions>(builder.Configuration.GetSection("TrieRefresh"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
    if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        throw new InvalidOperationException(
            "Mongo:ConnectionString is empty. Set via user-secrets:\n" +
            "  dotnet user-secrets set \"Mongo:ConnectionString\" \"<connection>\" --project src/SearchEngine.Api");
    return new MongoClient(opts.ConnectionString);
});

builder.Services.AddSingleton<ISearchService, MongoSearchService>();
builder.Services.AddSingleton<TrieIndex>();
builder.Services.AddHostedService<TrieRefreshService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SearchEngine API",
        Version = "v1",
        Description = "Endpoints de busca, autocomplete e suggest sobre o índice invertido alimentado pelo SearchEngine.Crawler."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchEngine API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "SearchEngine API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapSearchEndpoints();

await app.RunAsync();
