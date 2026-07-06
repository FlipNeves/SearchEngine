using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SearchEngine.Benchmark;
using SearchEngine.Shared.Dtos;

Console.OutputEncoding = Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

var options = configuration.GetSection("Benchmark").Get<BenchmarkOptions>() ?? new BenchmarkOptions();
if (options.Engines.Count != 2)
{
    Console.Error.WriteLine("Benchmark requires exactly 2 engines configured.");
    return 1;
}

var queries = new (string Label, string Text)[]
{
    ("frase pt", "linguagem de programação"),
    ("frase pt sem acento", "linguagem de programacao"),
    ("frase pt", "sistema operacional"),
    ("frase pt", "biblioteca de classes"),
    ("pt acentuada", "máquina virtual"),
    ("pt", "tutorial introdutório"),
    ("pergunta pt", "como instalar o dotnet"),
    ("en", "what is dotnet"),
    ("en", "cloud infrastructure"),
    ("en", "generative ai"),
    ("termo técnico", "C#"),
    ("termo técnico", ".NET"),
    ("termo técnico", "asp.net core"),
    ("termo único", "dotnet"),
    ("typo pt", "lingagem"),
    ("typo acento", "programaçao"),
};

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var first = options.Engines[0];
var second = options.Engines[1];

foreach (var engine in options.Engines)
{
    Console.Error.WriteLine($"Waiting for {engine.Name} at {engine.BaseUrl}...");
    await WaitUntilReadyAsync(engine);
}

var pooled = options.Engines.ToDictionary(e => e.Key, _ => new List<double>());
var overlaps = new List<double>();
var table = new StringBuilder();
table.AppendLine($"| Fenômeno | Query | {first.Name} p50/p95 (ms) | {second.Name} p50/p95 (ms) | Overlap@{options.Top} |");
table.AppendLine("|---|---|---|---|---|");

for (var q = 0; q < queries.Length; q++)
{
    var (label, text) = queries[q];
    Console.Error.WriteLine($"[{q + 1}/{queries.Length}] {text}");

    foreach (var engine in options.Engines)
        for (var w = 0; w < options.Warmup; w++)
            await MeasureAsync(engine, text);

    var latencies = options.Engines.ToDictionary(e => e.Key, _ => new List<double>());
    var topUrls = new Dictionary<string, IReadOnlyList<string>>();

    for (var i = 0; i < options.Iterations; i++)
        foreach (var engine in options.Engines)
        {
            var (ms, body) = await MeasureAsync(engine, text);
            latencies[engine.Key].Add(ms);
            if (body is not null && !topUrls.ContainsKey(engine.Key))
                topUrls[engine.Key] = body.Results.Select(r => r.Url).ToArray();
        }

    foreach (var engine in options.Engines)
        pooled[engine.Key].AddRange(latencies[engine.Key]);

    var overlap = Jaccard(
        topUrls.GetValueOrDefault(first.Key, []),
        topUrls.GetValueOrDefault(second.Key, []));
    overlaps.Add(overlap);

    table.AppendLine(
        $"| {label} | `{text}` " +
        $"| {Format(latencies[first.Key])} " +
        $"| {Format(latencies[second.Key])} " +
        $"| {overlap:F2} |");
}

table.AppendLine(
    $"| **agregado** | {queries.Length} queries × {options.Iterations} iterações " +
    $"| **{Format(pooled[first.Key])}** " +
    $"| **{Format(pooled[second.Key])}** " +
    $"| média {overlaps.Average():F2} |");

Console.WriteLine();
Console.WriteLine($"# Benchmark A/B — {first.Name} vs {second.Name}");
Console.WriteLine();
Console.WriteLine($"Config: {options.Iterations} iterações por query (+{options.Warmup} warmup descartadas), top={options.Top}, chamadas alternadas entre motores.");
Console.WriteLine("Ambiente: APIs locais contra o mesmo cluster Atlas M0 (free tier) via internet — números ilustram comportamento relativo, não rigor científico.");
Console.WriteLine();
Console.Write(table);
return 0;

async Task WaitUntilReadyAsync(EngineTarget engine)
{
    var deadline = DateTime.UtcNow.AddSeconds(90);
    while (true)
    {
        try
        {
            var response = await http.GetAsync($"{engine.BaseUrl}/search?q=net&top=1");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }

        if (DateTime.UtcNow > deadline)
            throw new TimeoutException($"Engine {engine.Name} not reachable at {engine.BaseUrl}");
        await Task.Delay(2000);
    }
}

async Task<(double Ms, SearchResponseDto? Body)> MeasureAsync(EngineTarget engine, string query)
{
    var url = $"{engine.BaseUrl}/search?q={Uri.EscapeDataString(query)}&top={options.Top}";
    var stopwatch = Stopwatch.StartNew();
    var response = await http.GetAsync(url);
    var body = response.IsSuccessStatusCode
        ? await response.Content.ReadFromJsonAsync<SearchResponseDto>(json)
        : null;
    stopwatch.Stop();
    return (stopwatch.Elapsed.TotalMilliseconds, body);
}

static string Format(List<double> latencies)
{
    var sorted = latencies.Order().ToList();
    return $"{Percentile(sorted, 0.50):F0} / {Percentile(sorted, 0.95):F0}";
}

static double Percentile(List<double> sorted, double p)
{
    if (sorted.Count == 0) return 0;
    var index = (int)Math.Ceiling(p * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

static double Jaccard(IReadOnlyList<string> a, IReadOnlyList<string> b)
{
    if (a.Count == 0 && b.Count == 0) return 1.0;
    var setA = a.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var setB = b.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var union = setA.Union(setB).Count();
    return union == 0 ? 0 : (double)setA.Intersect(setB).Count() / union;
}
