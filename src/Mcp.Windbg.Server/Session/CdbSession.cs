using System.Diagnostics;
using System.Text;

namespace Mcp.Windbg.Server.Session;

/// <summary>
/// Represents a single CDB.exe debugging session with marker-based command completion.
/// Supports both dump file analysis (-z) and remote debugging (-remote) modes.
/// </summary>
public sealed class CdbSession : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly SemaphoreSlim _commandSemaphore = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();
    private volatile bool _disposed;

    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the session type (Dump or Remote).
    /// </summary>
    public SessionType Type { get; }

    /// <summary>
    /// Gets the target (dump file path or remote connection string).
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; }

    /// <summary>
    /// Gets the timestamp of the last command executed.
    /// </summary>
    public DateTime LastActivityUtc { get; private set; }

    /// <summary>
    /// Gets whether the session is currently idle (no recent activity).
    /// </summary>
    public bool IsIdle => DateTime.UtcNow - LastActivityUtc > TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets whether the session is still active.
    /// </summary>
    public bool IsActive => !_disposed && !_process.HasExited;

    private CdbSession(string sessionId, SessionType type, string target, Process process, StreamWriter stdin)
    {
        SessionId = sessionId;
        Type = type;
        Target = target;
        CreatedUtc = DateTime.UtcNow;
        LastActivityUtc = DateTime.UtcNow;
        _process = process;
        _stdin = stdin;

        // Start async output reader
        _ = Task.Run(ReadOutputAsync, _disposalCts.Token);
    }

    /// <summary>
    /// Creates a new CDB session for analyzing a dump file.
    /// </summary>
    /// <param name="dumpPath">Path to the dump file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new CDB session</returns>
    public static async Task<CdbSession> CreateDumpSessionAsync(string dumpPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dumpPath))
            throw new ArgumentException("Dump path cannot be null or empty", nameof(dumpPath));

        if (!File.Exists(dumpPath))
            throw new FileNotFoundException($"Dump file not found: {dumpPath}");

        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cdb.exe",
                Arguments = $"-z \"{dumpPath}\" -c \".echo Session {sessionId} ready\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Failed to start CDB process");

            var stdin = process.StandardInput;
            var session = new CdbSession(sessionId, SessionType.Dump, dumpPath, process, stdin);

            // Wait for initial ready marker
            await session.WaitForMarkerAsync($"Session {sessionId} ready", cancellationToken);

            return session;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a new CDB session for remote debugging.
    /// </summary>
    /// <param name="connectionString">Remote connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new CDB session</returns>
    public static async Task<CdbSession> CreateRemoteSessionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cdb.exe",
                Arguments = $"-remote {connectionString} -c \".echo Session {sessionId} ready\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Failed to start CDB process");

            var stdin = process.StandardInput;
            var session = new CdbSession(sessionId, SessionType.Remote, connectionString, process, stdin);

            // Wait for initial ready marker
            await session.WaitForMarkerAsync($"Session {sessionId} ready", cancellationToken);

            return session;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Executes a command in the CDB session and returns the output.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="timeoutSeconds">Command timeout in seconds (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command output</returns>
    public async Task<string> ExecuteCommandAsync(string command, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CdbSession));

        if (!IsActive)
            throw new InvalidOperationException("Session is not active");

        await _commandSemaphore.WaitAsync(cancellationToken);
        try
        {
            LastActivityUtc = DateTime.UtcNow;

            var marker = Guid.NewGuid().ToString("N");
            var commandWithMarker = $"{command}\n.echo COMMAND_COMPLETE_{marker}";

            // Clear output buffer
            lock (_bufferLock)
            {
                _outputBuffer.Clear();
            }

            // Send command
            await _stdin.WriteLineAsync(commandWithMarker);
            await _stdin.FlushAsync();

            // Wait for completion marker
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await WaitForMarkerAsync($"COMMAND_COMPLETE_{marker}", combinedCts.Token);

            // Extract output (excluding the marker line)
            lock (_bufferLock)
            {
                var output = _outputBuffer.ToString();
                var markerIndex = output.LastIndexOf($"COMMAND_COMPLETE_{marker}");
                if (markerIndex >= 0)
                {
                    output = output[..markerIndex].TrimEnd();
                }
                return output;
            }
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }

    private async Task WaitForMarkerAsync(string marker, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(50, cancellationToken);

            lock (_bufferLock)
            {
                if (_outputBuffer.ToString().Contains(marker))
                    return;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task ReadOutputAsync()
    {
        try
        {
            var buffer = new char[4096];
            var reader = _process.StandardOutput;

            while (!_disposalCts.Token.IsCancellationRequested && !_process.HasExited)
            {
                var bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                lock (_bufferLock)
                {
                    _outputBuffer.Append(buffer, 0, bytesRead);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected when session is disposed
        }
        catch (Exception)
        {
            // Log error but don't throw - this runs in background
        }
    }

    /// <summary>
    /// Closes the CDB session gracefully.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Send quit command if process is still running
            if (!_process.HasExited)
            {
                _stdin.WriteLine("q");
                _stdin.Flush();

                // Give process time to exit gracefully
                if (!_process.WaitForExit(5000))
                {
                    _process.Kill();
                }
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
        finally
        {
            _disposalCts.Cancel();
            _disposalCts.Dispose();
            _commandSemaphore.Dispose();
            _stdin.Dispose();
            _process.Dispose();
        }
    }
}

/// <summary>
/// Represents the type of CDB session.
/// </summary>
public enum SessionType
{
    /// <summary>
    /// Dump file analysis session.
    /// </summary>
    Dump,

    /// <summary>
    /// Remote debugging session.
    /// </summary>
    Remote
}