using Microsoft.OpenApi.Models;
using SearchEngine.FromScratch.Api.Endpoints;
using SearchEngine.FromScratch.Api.Options;
using SearchEngine.FromScratch.Api.Services;
using SearchEngine.FromScratch.Infrastructure.Daos;
using SearchEngine.FromScratch.Infrastructure.Searching;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<TrieRefreshOptions>(builder.Configuration.GetSection("TrieRefresh"));

builder.Services.AddSharedMongo();
builder.Services.AddScoped<IInvertedIndexDao, InvertedIndexDao>();
builder.Services.AddScoped<ISearchEngine, FromScratchSearchEngine>();

builder.Services.AddSingleton<TrieIndex>();
builder.Services.AddHostedService<TrieRefreshService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SearchEngine FromScratch API",
        Version = "v1",
        Description = "Endpoints de busca, autocomplete e suggest sobre o índice invertido construído à mão."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchEngine FromScratch API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "SearchEngine FromScratch API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapSearchEndpoints();

await app.RunAsync();
