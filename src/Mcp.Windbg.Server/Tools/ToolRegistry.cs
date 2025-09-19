using System.Collections.Concurrent;

namespace Mcp.Windbg.Server.Tools;

public sealed class ToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry Register(ITool tool)
    {
        _tools[tool.Name] = tool;
        return this;
    }

    public IEnumerable<ITool> List() => _tools.Values.OrderBy(t => t.Name);

    public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);
}
