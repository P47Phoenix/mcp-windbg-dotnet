using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for opening remote debugging sessions.
/// </summary>
public sealed class OpenRemoteTool : ITool
{
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Initializes the open remote tool with the specified session repository.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    public OpenRemoteTool(SessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc/>
    public string Name => "open_remote";

    /// <inheritdoc/>
    public string Description => "Open a remote debugging session. Returns session ID for subsequent commands.";

    /// <inheritdoc/>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        // Parse arguments
        if (args == null)
            throw new ArgumentException("Missing arguments. Required: connectionString");

        var connectionString = args["connectionString"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Missing or empty connectionString argument");

        try
        {
            // Create the session
            var session = await _sessionRepository.CreateRemoteSessionAsync(connectionString, ct);

            // Return session information
            var result = new JsonObject
            {
                ["success"] = true,
                ["sessionId"] = session.SessionId,
                ["sessionType"] = session.Type.ToString(),
                ["target"] = session.Target,
                ["createdUtc"] = session.CreatedUtc.ToString("O"),
                ["message"] = $"Successfully opened remote session {session.SessionId}"
            };

            return result;
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