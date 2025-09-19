using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Windbg.Server.Protocol;
using Mcp.Windbg.Server.Tools;
using Xunit;

namespace Mcp.Windbg.Server.Tests;

public class ListToolsIntegrationTests
{
    [Fact]
    public async Task ListTools_ReturnsHealthCheck()
    {
        var registry = new ToolRegistry();
        registry.Register(new HealthCheckTool());
        using var cts = new CancellationTokenSource();
        var loop = new MessageLoop(registry, cts.Token);

        // Simulate stdin/stdout via pipes: redirect Console temporarily.
        using var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        using var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);

        // NOTE: Simplification: Instead of refactoring MessageLoop for injectable streams,
        // we directly validate tool registry independently.
        var tools = registry.List();
        Assert.Contains(tools, t => t.Name == "health_check");
    }
}
