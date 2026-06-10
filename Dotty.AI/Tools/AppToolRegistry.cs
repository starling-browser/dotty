namespace Dotty.AI.Tools;

public class AppToolRegistry
{
    private readonly Dictionary<string, IAppTool> _tools = new();

    public void Register(IAppTool tool) => _tools[tool.Name] = tool;

    public IAppTool? GetTool(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    public IReadOnlyCollection<IAppTool> AllTools => _tools.Values;
}
