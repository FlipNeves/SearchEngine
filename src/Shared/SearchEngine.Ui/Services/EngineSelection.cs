using Microsoft.Extensions.Options;
using SearchEngine.Ui.Options;

namespace SearchEngine.Ui.Services;

public sealed class EngineSelection
{
    public EngineSelection(IOptions<ApiOptions> options)
    {
        Engines = options.Value.Engines;
        Current = Engines.Count > 0 ? Engines[0] : new EngineOption();
    }

    public IReadOnlyList<EngineOption> Engines { get; }

    public EngineOption Current { get; private set; }

    public void Select(string key)
    {
        var engine = Engines.FirstOrDefault(e => e.Key == key);
        if (engine is not null)
            Current = engine;
    }
}
