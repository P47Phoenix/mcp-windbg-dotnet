using System.Text;
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

        var inputBuilder = new StringBuilder();
        inputBuilder.AppendLine("{\"method\":\"list_tools\"}");
        using var input = new StringReader(inputBuilder.ToString());
        var outputBuilder = new StringBuilder();
        using var output = new StringWriter(outputBuilder);

        var loop = new MessageLoop(registry, input, output, cts.Token);
        await loop.RunAsync();

        var lines = outputBuilder.ToString()
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("health_check", lines[0]);
    }
}
