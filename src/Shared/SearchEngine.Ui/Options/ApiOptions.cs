namespace SearchEngine.Ui.Options;

public sealed class ApiOptions
{
    public List<EngineOption> Engines { get; set; } = [];
}

public sealed class EngineOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
