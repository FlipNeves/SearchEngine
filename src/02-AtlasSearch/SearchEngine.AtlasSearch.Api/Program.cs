using Microsoft.OpenApi.Models;
using SearchEngine.AtlasSearch.Api.Endpoints;
using SearchEngine.AtlasSearch.Infrastructure;
using SearchEngine.AtlasSearch.Infrastructure.Indexing;
using SearchEngine.AtlasSearch.Infrastructure.Searching;
using SearchEngine.Shared.Domain.Interfaces;
using SearchEngine.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<AtlasSearchOptions>(builder.Configuration.GetSection("AtlasSearch"));

builder.Services.AddSharedMongo();
builder.Services.AddScoped<ISearchEngine, AtlasSearchEngine>();
builder.Services.AddHostedService<SearchIndexInitializer>();

const string UiCorsPolicy = "ui";
builder.Services.AddCors(opts => opts.AddPolicy(UiCorsPolicy, p => p
    .WithOrigins("http://localhost:5003")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SearchEngine AtlasSearch API",
        Version = "v1",
        Description = "Endpoints de busca sobre o MongoDB Atlas Search ($search / Lucene gerenciado)."
    });
});

var app = builder.Build();

app.UseCors(UiCorsPolicy);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SearchEngine AtlasSearch API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "SearchEngine AtlasSearch API";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapSearchEndpoints();

await app.RunAsync();
