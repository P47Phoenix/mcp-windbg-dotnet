namespace Mcp.Windbg.Server.Policy;

/// <summary>
/// Interface for command policy enforcement in CDB sessions.
/// </summary>
public interface ICommandPolicy
{
    /// <summary>
    /// Validates whether a command is allowed to be executed.
    /// </summary>
    /// <param name="command">The command to validate</param>
    /// <returns>A validation result indicating if the command is allowed</returns>
    CommandValidationResult ValidateCommand(string command);
}

/// <summary>
/// Result of command validation.
/// </summary>
public record CommandValidationResult
{
    /// <summary>
    /// Gets whether the command is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the reason why the command was denied (if applicable).
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Gets the policy rule that was applied.
    /// </summary>
    public string? PolicyRule { get; init; }

    /// <summary>
    /// Creates a result indicating the command is allowed.
    /// </summary>
    public static CommandValidationResult Allow() => new() { IsAllowed = true };

    /// <summary>
    /// Creates a result indicating the command is denied.
    /// </summary>
    /// <param name="reason">The reason for denial</param>
    /// <param name="policyRule">The policy rule that was applied</param>
    public static CommandValidationResult Deny(string reason, string? policyRule = null) =>
        new() { IsAllowed = false, DenialReason = reason, PolicyRule = policyRule };
}