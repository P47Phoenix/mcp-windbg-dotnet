using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;
using Mcp.Windbg.Server.Policy;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for executing commands in CDB sessions with policy enforcement.
/// </summary>
public sealed class RunCommandTool : ITool
{
    private readonly SessionRepository _sessionRepository;
    private readonly ICommandPolicy _commandPolicy;

    /// <summary>
    /// Initializes the run command tool with the specified dependencies.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    /// <param name="commandPolicy">Command policy for validation</param>
    public RunCommandTool(SessionRepository sessionRepository, ICommandPolicy commandPolicy)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _commandPolicy = commandPolicy ?? throw new ArgumentNullException(nameof(commandPolicy));
    }

    /// <inheritdoc/>
    public string Name => "run_command";

    /// <inheritdoc/>
    public string Description => "Execute a command in an existing debugging session with policy enforcement.";

    /// <inheritdoc/>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        // Parse arguments
        if (args == null)
            throw new ArgumentException("Missing arguments. Required: sessionId, command");

        var sessionId = args["sessionId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Missing or empty sessionId argument");

        var command = args["command"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Missing or empty command argument");

        var timeoutSeconds = args["timeoutSeconds"]?.GetValue<int>() ?? 30;

        try
        {
            // Get the session
            var session = _sessionRepository.GetSession(sessionId);
            if (session == null)
            {
                return new JsonObject
                {
                    ["success"] = false,
                    ["error"] = "SessionNotFound",
                    ["message"] = $"Session {sessionId} not found"
                };
            }

            if (!session.IsActive)
            {
                return new JsonObject
                {
                    ["success"] = false,
                    ["error"] = "SessionInactive",
                    ["message"] = $"Session {sessionId} is not active"
                };
            }

            // Validate command against policy
            var validation = _commandPolicy.ValidateCommand(command);
            if (!validation.IsAllowed)
            {
                return new JsonObject
                {
                    ["success"] = false,
                    ["error"] = "CommandDenied",
                    ["message"] = validation.DenialReason ?? "Command not allowed by policy",
                    ["policyRule"] = validation.PolicyRule,
                    ["command"] = command
                };
            }

            // Execute the command
            var startTime = DateTime.UtcNow;
            var output = await session.ExecuteCommandAsync(command, timeoutSeconds, ct);
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            return new JsonObject
            {
                ["success"] = true,
                ["sessionId"] = sessionId,
                ["command"] = command,
                ["output"] = output,
                ["executionTimeMs"] = (int)duration.TotalMilliseconds,
                ["timestamp"] = endTime.ToString("O")
            };
        }
        catch (TaskCanceledException)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "CommandTimeout",
                ["message"] = $"Command timed out after {timeoutSeconds} seconds",
                ["command"] = command,
                ["timeoutSeconds"] = timeoutSeconds
            };
        }
        catch (InvalidOperationException ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "SessionError",
                ["message"] = ex.Message,
                ["command"] = command
            };
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "CommandExecutionFailed",
                ["message"] = ex.Message,
                ["command"] = command
            };
        }
    }
}