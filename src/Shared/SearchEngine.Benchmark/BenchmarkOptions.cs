namespace SearchEngine.Benchmark;

public sealed class BenchmarkOptions
{
    public int Iterations { get; set; } = 20;
    public int Warmup { get; set; } = 3;
    public int Top { get; set; } = 10;
    public List<EngineTarget> Engines { get; set; } = [];
}

public sealed class EngineTarget
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
