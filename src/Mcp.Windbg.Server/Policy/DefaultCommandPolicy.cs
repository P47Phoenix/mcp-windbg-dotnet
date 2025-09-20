using System.Text.RegularExpressions;

namespace Mcp.Windbg.Server.Policy;

/// <summary>
/// Default command policy implementation with allowlist and denylist support.
/// </summary>
public sealed class DefaultCommandPolicy : ICommandPolicy
{
    private readonly HashSet<string> _allowedCommands;
    private readonly List<Regex> _allowedPatterns;
    private readonly List<Regex> _deniedPatterns;
    private readonly bool _allowByDefault;

    /// <summary>
    /// Initializes a new command policy with the specified configuration.
    /// </summary>
    /// <param name="config">Policy configuration</param>
    public DefaultCommandPolicy(CommandPolicyConfig config)
    {
        _allowByDefault = config.AllowByDefault;
        
        // Build allowed commands set
        _allowedCommands = new HashSet<string>(config.AllowedCommands, StringComparer.OrdinalIgnoreCase);
        
        // Compile allowed patterns
        _allowedPatterns = config.AllowedPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();
            
        // Compile denied patterns
        _deniedPatterns = config.DeniedPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();
    }

    /// <summary>
    /// Creates a default safe policy with common analysis commands.
    /// </summary>
    public static DefaultCommandPolicy CreateDefault()
    {
        var config = new CommandPolicyConfig
        {
            AllowByDefault = false,
            AllowedCommands = new[]
            {
                // Basic analysis commands
                ".lastevent",
                "!analyze",
                "!analyze -v",
                "k", "kb", "kn", "kp", "kv",
                "~", "~*", 
                "lm", "lmv", "lmf",
                "r", "rd", "rm",
                "!threads", "!runaway",
                "dt", "dv", "dx",
                "u", "ub", "uf",
                "ln", "x",
                "!peb", "!teb",
                "!heap", "!address",
                "!clrstack", "!pe", "!dumpheap",
                "!finalizequeue", "!gcroot",
                "!eeheap", "!dumpdomain",
                
                // Safe utility commands
                ".echo", ".help", "?", "??",
                "version", ".time", ".uptime",
                ".prefer_dml", ".dml_flow"
            },
            AllowedPatterns = new[]
            {
                @"^\.echo\s+.*",           // Echo commands
                @"^k[bnpv]?\s*\d*$",       // Stack traces with optional counts
                @"^~\d*[ks]?$",            // Thread commands
                @"^lm[vf]*\s*\w*$",        // Module list commands
                @"^r\s+\w+$",              // Register display for specific register
                @"^dt\s+[\w!:]+(\s+0x[0-9a-f]+)?$", // Display type commands
                @"^u\s+[0-9a-f]+(\s+L\d+)?$",      // Unassemble commands
                @"^!.*heap.*",             // Heap-related SOS commands
                @"^!.*clr.*",              // CLR-related SOS commands
                @"^!dumpheap\s*(-stat|-mt\s+0x[0-9a-f]+)?$" // Safe dumpheap variants
            },
            DeniedPatterns = new[]
            {
                @"\.shell",                // Shell execution
                @"\.cmd",                  // Command execution
                @"\.load",                 // Loading extensions (unless whitelisted)
                @"\.unload",               // Unloading extensions
                @"\.restart",              // Process restart
                @"\.crash",                // Crash process
                @"\.kill",                 // Kill process
                @"\.attach",               // Attach to process
                @"\.detach",               // Detach from process
                @"\.create",               // Create process
                @"\.open",                 // Open dump/process
                @"\.opendump",             // Open dump
                @"\.dump",                 // Create dump
                @"\.writemem",             // Write memory
                @"\.fillmem",              // Fill memory
                @"^e[abcdpqw]\s",          // Edit memory commands (anchor to start)
                @"^f\s",                   // Fill memory
                @"^as\s",                  // Assign string (could be misused)
                @"^al\s",                  // Assign local
                @"\$\$",                   // Script execution
                @"^bp\s",                  // Set breakpoint (anchor to start)
                @"^bc\s",                  // Clear breakpoint
                @"^g\s",                   // Go (execution)
                @"^t\s",                   // Trace
                @"^p\s",                   // Step
                @"^gu\s"                   // Go up
            }
        };

        return new DefaultCommandPolicy(config);
    }

    /// <inheritdoc/>
    public CommandValidationResult ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return CommandValidationResult.Deny("Empty command", "EmptyCommand");

        var trimmedCommand = command.Trim();
        
        // Check denied patterns first (highest priority)
        foreach (var deniedPattern in _deniedPatterns)
        {
            if (deniedPattern.IsMatch(trimmedCommand))
            {
                return CommandValidationResult.Deny(
                    $"Command matches denied pattern: {deniedPattern.ToString()}", 
                    "DeniedPattern");
            }
        }

        // Check allowed commands list
        if (_allowedCommands.Contains(trimmedCommand))
        {
            return CommandValidationResult.Allow();
        }

        // Check allowed patterns
        foreach (var allowedPattern in _allowedPatterns)
        {
            if (allowedPattern.IsMatch(trimmedCommand))
            {
                return CommandValidationResult.Allow();
            }
        }

        // Default behavior
        if (_allowByDefault)
        {
            return CommandValidationResult.Allow();
        }

        return CommandValidationResult.Deny(
            "Command not in allowlist", 
            "NotInAllowlist");
    }
}

/// <summary>
/// Configuration for command policy.
/// </summary>
public record CommandPolicyConfig
{
    /// <summary>
    /// Whether to allow commands by default if not explicitly denied.
    /// </summary>
    public bool AllowByDefault { get; init; } = false;

    /// <summary>
    /// List of explicitly allowed commands.
    /// </summary>
    public string[] AllowedCommands { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Regular expression patterns for allowed commands.
    /// </summary>
    public string[] AllowedPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Regular expression patterns for denied commands (takes precedence).
    /// </summary>
    public string[] DeniedPatterns { get; init; } = Array.Empty<string>();
}