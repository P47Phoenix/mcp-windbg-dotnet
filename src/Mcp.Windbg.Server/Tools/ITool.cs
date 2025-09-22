using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Mcp.Windbg.Server.Tools;

/// <summary>
/// Legacy untyped tool interface kept temporarily for backward compatibility with the message loop.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct);
}

/// <summary>
/// Strongly typed tool interface defining contracts for request and response payloads.
/// </summary>
/// <typeparam name="TArgs">Input arguments contract</typeparam>
/// <typeparam name="TResult">Result contract</typeparam>
public interface ITool<TArgs, TResult> : ITool where TArgs : class where TResult : class
{
    /// <summary>
    /// Execute the tool with typed arguments.
    /// </summary>
    Task<TResult> ExecuteTypedAsync(TArgs args, CancellationToken ct);
}

/// <summary>
/// Helper base class to reduce boilerplate for implementing strongly typed tools while still satisfying the legacy interface.
/// </summary>
public abstract class ToolBase<TArgs, TResult> : ITool<TArgs, TResult>
    where TArgs : class
    where TResult : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract Task<TResult> ExecuteTypedAsync(TArgs args, CancellationToken ct);

    /// <summary>
    /// Adapts the legacy JsonNode invocation into the typed contract.
    /// </summary>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        if (args == null)
        {
            throw new ArgumentException("Arguments payload required");
        }
        // Deserialize into typed args
    var typed = System.Text.Json.JsonSerializer.Deserialize<TArgs>(args.ToJsonString())
            ?? throw new ArgumentException("Failed to deserialize arguments to contract '" + typeof(TArgs).Name + "'");
        var result = await ExecuteTypedAsync(typed, ct).ConfigureAwait(false);
        // Serialize back to node for outer protocol
        return System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(result))!;
    }
}
