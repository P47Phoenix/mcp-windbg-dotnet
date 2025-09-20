using System.Text;
using System.Text.Json;
using Mcp.Windbg.Server.Configuration;
using Mcp.Windbg.Server.Protocol;
using Mcp.Windbg.Server.Tools;
using Mcp.Windbg.Server.Tools.Session;
using Mcp.Windbg.Server.Tools.Analysis;
using Mcp.Windbg.Server.Session;
using Mcp.Windbg.Server.Policy;
using Xunit;

namespace Mcp.Windbg.Server.Tests.Integration;

/// <summary>
/// Integration tests for the complete WinDBG MCP workflow.
/// </summary>
public class WinDbgWorkflowIntegrationTests
{
    [Fact]
    public async Task CompleteWorkflow_ToolRegistration_ReturnsAllExpectedTools()
    {
        // Arrange - Set up the complete service stack like in Program.cs
        var sessionRepository = new SessionRepository(5, 10);
        var commandPolicy = DefaultCommandPolicy.CreateDefault();
        
        var registry = new ToolRegistry()
            .Register(new HealthCheckTool())
            .Register(new OpenDumpTool(sessionRepository))
            .Register(new OpenRemoteTool(sessionRepository))
            .Register(new CloseDumpTool(sessionRepository))
            .Register(new RunCommandTool(sessionRepository, commandPolicy))
            .Register(new ListDumpsTool())
            .Register(new SessionInfoTool(sessionRepository))
            .Register(new AnalyzeDumpTool(sessionRepository));

        using var cts = new CancellationTokenSource();
        var inputBuilder = new StringBuilder();
        inputBuilder.AppendLine("{\"method\":\"list_tools\"}");
        
        using var input = new StringReader(inputBuilder.ToString());
        var outputBuilder = new StringBuilder();
        using var output = new StringWriter(outputBuilder);

        // Act
        var loop = new MessageLoop(registry, input, output, cts.Token);
        await loop.RunAsync();

        // Assert
        var lines = outputBuilder.ToString()
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Single(lines);
        var response = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.True(response.GetProperty("ok").GetBoolean());
        
        var tools = response.GetProperty("result").EnumerateArray().ToList();
        Assert.Equal(8, tools.Count);
        
        // Verify specific tools are present
        var toolNames = tools.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("health_check", toolNames);
        Assert.Contains("open_dump", toolNames);
        Assert.Contains("open_remote", toolNames);
        Assert.Contains("close_dump", toolNames);
        Assert.Contains("run_command", toolNames);
        Assert.Contains("analyze_dump", toolNames);
        Assert.Contains("list_dumps", toolNames);
        Assert.Contains("session_info", toolNames);

        // Cleanup
        sessionRepository.Dispose();
    }

    [Fact]
    public async Task HealthCheck_ExecutesSuccessfully()
    {
        // Arrange
        var registry = new ToolRegistry()
            .Register(new HealthCheckTool());

        using var cts = new CancellationTokenSource();
        var inputBuilder = new StringBuilder();
        inputBuilder.AppendLine("{\"method\":\"call_tool\",\"name\":\"health_check\",\"args\":{}}");
        
        using var input = new StringReader(inputBuilder.ToString());
        var outputBuilder = new StringBuilder();
        using var output = new StringWriter(outputBuilder);

        // Act
        var loop = new MessageLoop(registry, input, output, cts.Token);
        await loop.RunAsync();

        // Assert
        var lines = outputBuilder.ToString()
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Single(lines);
        var response = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.True(response.GetProperty("ok").GetBoolean());
        
        var result = response.GetProperty("result");
        Assert.Equal("ok", result.GetProperty("status").GetString());
        Assert.True(result.TryGetProperty("serverVersion", out _));
        Assert.True(result.TryGetProperty("implementation", out _));
        Assert.True(result.TryGetProperty("uptimeSeconds", out _));
    }

    [Fact]
    public async Task SessionInfo_WithNoSessions_ReturnsEmptyStatistics()
    {
        // Arrange
        var sessionRepository = new SessionRepository(5, 10);
        var registry = new ToolRegistry()
            .Register(new SessionInfoTool(sessionRepository));

        using var cts = new CancellationTokenSource();
        var inputBuilder = new StringBuilder();
        inputBuilder.AppendLine("{\"method\":\"call_tool\",\"name\":\"session_info\",\"args\":{}}");
        
        using var input = new StringReader(inputBuilder.ToString());
        var outputBuilder = new StringBuilder();
        using var output = new StringWriter(outputBuilder);

        // Act
        var loop = new MessageLoop(registry, input, output, cts.Token);
        await loop.RunAsync();

        // Assert
        var lines = outputBuilder.ToString()
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        Assert.Single(lines);
        var response = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.True(response.GetProperty("ok").GetBoolean());
        
        var result = response.GetProperty("result");
        Assert.True(result.GetProperty("success").GetBoolean());
        
        var statistics = result.GetProperty("statistics");
        Assert.Equal(0, statistics.GetProperty("totalSessions").GetInt32());
        Assert.Equal(5, statistics.GetProperty("maxConcurrentSessions").GetInt32());
        
        var sessions = result.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Empty(sessions);

        // Cleanup
        sessionRepository.Dispose();
    }
}