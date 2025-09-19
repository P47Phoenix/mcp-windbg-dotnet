using Mcp.Windbg.Server.Tools;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Mcp.Windbg.Server.Tests;

public class HealthCheckToolTests
{
    [Fact]
    public async Task Health_ReturnsExpectedFields()
    {
        var tool = new HealthCheckTool();
    var result = await tool.ExecuteAsync(null, CancellationToken.None) as JsonObject;
        Assert.NotNull(result);
        Assert.Equal("ok", (string?)result!["status"]);
        Assert.NotNull(result["serverVersion"]);
        Assert.NotNull(result["timestampUtc"]);
        Assert.NotNull(result["uptimeSeconds"]);
    }
}
