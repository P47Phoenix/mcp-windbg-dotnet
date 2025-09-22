using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for closing dump or remote debugging sessions.
/// </summary>
public sealed class CloseDumpTool : ITool
{
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Initializes the close dump tool with the specified session repository.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    public CloseDumpTool(SessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc/>
    public string Name => "close_dump";

    /// <inheritdoc/>
    public string Description => "Close a debugging session by session ID. Works for both dump and remote sessions.";

    /// <inheritdoc/>
    public Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        // Parse arguments
        if (args == null)
            throw new ArgumentException("Missing arguments. Required: sessionId");

        var sessionId = args["sessionId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Missing or empty sessionId argument");

        try
        {
            // Get session info before closing
            var session = _sessionRepository.GetSession(sessionId);
            if (session == null)
            {
                return Task.FromResult<JsonNode>(new JsonObject
                {
                    ["success"] = false,
                    ["error"] = "SessionNotFound",
                    ["message"] = $"Session {sessionId} not found"
                });
            }

            var sessionInfo = new JsonObject
            {
                ["sessionId"] = session.SessionId,
                ["sessionType"] = session.Type.ToString(),
                ["target"] = session.Target,
                ["createdUtc"] = session.CreatedUtc.ToString("O"),
                ["lastActivityUtc"] = session.LastActivityUtc.ToString("O"),
                ["wasActive"] = session.IsActive
            };

            // Close the session
            var closed = _sessionRepository.CloseSession(sessionId);
            
            if (closed)
            {
                return Task.FromResult<JsonNode>(new JsonObject
                {
                    ["success"] = true,
                    ["message"] = $"Successfully closed session {sessionId}",
                    ["sessionInfo"] = sessionInfo
                });
            }
            else
            {
                return Task.FromResult<JsonNode>(new JsonObject
                {
                    ["success"] = false,
                    ["error"] = "SessionCloseFailed", 
                    ["message"] = $"Failed to close session {sessionId}",
                    ["sessionInfo"] = sessionInfo
                });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["success"] = false,
                ["error"] = "SessionCloseError",
                ["message"] = ex.Message
            });
        }
    }
}