using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for opening dump file sessions for analysis.
/// </summary>
public sealed class OpenDumpTool : ITool
{
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Initializes the open dump tool with the specified session repository.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    public OpenDumpTool(SessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc/>
    public string Name => "open_dump";

    /// <inheritdoc/>
    public string Description => "Open a crash dump file for analysis. Returns session ID for subsequent commands.";

    /// <inheritdoc/>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        // Parse arguments
        if (args == null)
            throw new ArgumentException("Missing arguments. Required: dumpPath");

        var dumpPath = args["dumpPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(dumpPath))
            throw new ArgumentException("Missing or empty dumpPath argument");

        try
        {
            // Create the session
            var session = await _sessionRepository.CreateDumpSessionAsync(dumpPath, ct);

            // Return session information
            var result = new JsonObject
            {
                ["success"] = true,
                ["sessionId"] = session.SessionId,
                ["sessionType"] = session.Type.ToString(),
                ["target"] = session.Target,
                ["createdUtc"] = session.CreatedUtc.ToString("O"),
                ["message"] = $"Successfully opened dump session {session.SessionId}"
            };

            return result;
        }
        catch (FileNotFoundException ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "DumpFileNotFound",
                ["message"] = ex.Message
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Maximum concurrent sessions"))
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "SessionLimitExceeded",
                ["message"] = ex.Message,
                ["maxSessions"] = _sessionRepository.MaxConcurrentSessions
            };
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "SessionCreationFailed",
                ["message"] = ex.Message
            };
        }
    }
}