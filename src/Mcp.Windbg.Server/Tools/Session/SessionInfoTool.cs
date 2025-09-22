using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for getting information about active debugging sessions.
/// </summary>
public sealed class SessionInfoTool : ITool
{
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Initializes the session info tool with the specified session repository.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    public SessionInfoTool(SessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc/>
    public string Name => "session_info";

    /// <inheritdoc/>
    public string Description => "Get information about active debugging sessions and repository statistics.";

    /// <inheritdoc/>
    public Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        try
        {
            var sessionId = args?["sessionId"]?.GetValue<string>();
            
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                // Get specific session info
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

                return Task.FromResult<JsonNode>(new JsonObject
                {
                    ["success"] = true,
                    ["session"] = CreateSessionJson(session)
                });
            }
            else
            {
                // Get all sessions and statistics
                var sessions = _sessionRepository.GetAllSessions();
                var statistics = _sessionRepository.GetStatistics();

                return Task.FromResult<JsonNode>(new JsonObject
                {
                    ["success"] = true,
                    ["statistics"] = CreateStatisticsJson(statistics),
                    ["sessions"] = new JsonArray(sessions.Select(CreateSessionJson).ToArray())
                });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["success"] = false,
                ["error"] = "SessionInfoError",
                ["message"] = ex.Message
            });
        }
    }

    private static JsonObject CreateSessionJson(CdbSession session)
    {
        var now = DateTime.UtcNow;
        var age = now - session.CreatedUtc;
        var idleTime = now - session.LastActivityUtc;

        return new JsonObject
        {
            ["sessionId"] = session.SessionId,
            ["type"] = session.Type.ToString(),
            ["target"] = session.Target,
            ["isActive"] = session.IsActive,
            ["isIdle"] = session.IsIdle,
            ["createdUtc"] = session.CreatedUtc.ToString("O"),
            ["lastActivityUtc"] = session.LastActivityUtc.ToString("O"),
            ["ageMinutes"] = Math.Round(age.TotalMinutes, 1),
            ["idleMinutes"] = Math.Round(idleTime.TotalMinutes, 1)
        };
    }

    private static JsonObject CreateStatisticsJson(SessionStatistics stats)
    {
        return new JsonObject
        {
            ["totalSessions"] = stats.TotalSessions,
            ["activeSessions"] = stats.ActiveSessions,
            ["idleSessions"] = stats.IdleSessions,
            ["dumpSessions"] = stats.DumpSessions,
            ["remoteSessions"] = stats.RemoteSessions,
            ["maxConcurrentSessions"] = stats.MaxConcurrentSessions,
            ["idleTimeoutMinutes"] = stats.IdleTimeoutMinutes,
            ["utilizationPercent"] = stats.MaxConcurrentSessions > 0 
                ? Math.Round(100.0 * stats.TotalSessions / stats.MaxConcurrentSessions, 1) 
                : 0
        };
    }
}