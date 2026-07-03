using SearchEngine.Ui.Components;
using SearchEngine.Ui.Options;
using SearchEngine.Ui.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<EngineSelection>();
builder.Services.AddHttpClient<ISearchApi, SearchApiClient>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
