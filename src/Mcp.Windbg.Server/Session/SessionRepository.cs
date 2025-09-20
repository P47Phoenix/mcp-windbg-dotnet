using System.Collections.Concurrent;

namespace Mcp.Windbg.Server.Session;

/// <summary>
/// Thread-safe repository for managing CDB sessions with automatic cleanup.
/// </summary>
public sealed class SessionRepository : IDisposable
{
    private readonly ConcurrentDictionary<string, CdbSession> _sessions = new();
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    private volatile bool _disposed;

    /// <summary>
    /// Gets the maximum number of concurrent sessions allowed.
    /// </summary>
    public int MaxConcurrentSessions { get; }

    /// <summary>
    /// Gets the idle timeout in minutes before sessions are automatically evicted.
    /// </summary>
    public int IdleTimeoutMinutes { get; }

    /// <summary>
    /// Initializes a new session repository with the specified limits.
    /// </summary>
    /// <param name="maxConcurrentSessions">Maximum concurrent sessions (default: 5)</param>
    /// <param name="idleTimeoutMinutes">Idle timeout in minutes (default: 10)</param>
    public SessionRepository(int maxConcurrentSessions = 5, int idleTimeoutMinutes = 10)
    {
        MaxConcurrentSessions = maxConcurrentSessions;
        IdleTimeoutMinutes = idleTimeoutMinutes;

        // Run cleanup every minute
        _cleanupTimer = new Timer(CleanupIdleSessions, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Creates a new dump analysis session.
    /// </summary>
    /// <param name="dumpPath">Path to the dump file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session</returns>
    /// <exception cref="InvalidOperationException">Thrown when session limit is exceeded</exception>
    public async Task<CdbSession> CreateDumpSessionAsync(string dumpPath, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        // Check session limits
        if (_sessions.Count >= MaxConcurrentSessions)
        {
            // Try to clean up idle sessions first
            await CleanupIdleSessionsAsync();
            
            if (_sessions.Count >= MaxConcurrentSessions)
                throw new InvalidOperationException($"Maximum concurrent sessions limit ({MaxConcurrentSessions}) exceeded");
        }

        var session = await CdbSession.CreateDumpSessionAsync(dumpPath, cancellationToken);
        
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            session.Dispose();
            throw new InvalidOperationException("Failed to register session (duplicate ID)");
        }

        return session;
    }

    /// <summary>
    /// Creates a new remote debugging session.
    /// </summary>
    /// <param name="connectionString">Remote connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session</returns>
    /// <exception cref="InvalidOperationException">Thrown when session limit is exceeded</exception>
    public async Task<CdbSession> CreateRemoteSessionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        // Check session limits
        if (_sessions.Count >= MaxConcurrentSessions)
        {
            // Try to clean up idle sessions first
            await CleanupIdleSessionsAsync();
            
            if (_sessions.Count >= MaxConcurrentSessions)
                throw new InvalidOperationException($"Maximum concurrent sessions limit ({MaxConcurrentSessions}) exceeded");
        }

        var session = await CdbSession.CreateRemoteSessionAsync(connectionString, cancellationToken);
        
        if (!_sessions.TryAdd(session.SessionId, session))
        {
            session.Dispose();
            throw new InvalidOperationException("Failed to register session (duplicate ID)");
        }

        return session;
    }

    /// <summary>
    /// Gets a session by its ID.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The session if found, null otherwise</returns>
    public CdbSession? GetSession(string sessionId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    /// <returns>A collection of all active sessions</returns>
    public IReadOnlyCollection<CdbSession> GetAllSessions()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        return _sessions.Values.ToList();
    }

    /// <summary>
    /// Closes and removes a session by its ID.
    /// </summary>
    /// <param name="sessionId">The session ID to close</param>
    /// <returns>True if the session was found and closed, false otherwise</returns>
    public bool CloseSession(string sessionId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets session statistics.
    /// </summary>
    /// <returns>Session statistics</returns>
    public SessionStatistics GetStatistics()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SessionRepository));

        var sessions = _sessions.Values.ToList();
        var activeSessions = sessions.Count(s => s.IsActive);
        var idleSessions = sessions.Count(s => s.IsIdle);
        var dumpSessions = sessions.Count(s => s.Type == SessionType.Dump);
        var remoteSessions = sessions.Count(s => s.Type == SessionType.Remote);

        return new SessionStatistics
        {
            TotalSessions = sessions.Count,
            ActiveSessions = activeSessions,
            IdleSessions = idleSessions,
            DumpSessions = dumpSessions,
            RemoteSessions = remoteSessions,
            MaxConcurrentSessions = MaxConcurrentSessions,
            IdleTimeoutMinutes = IdleTimeoutMinutes
        };
    }

    private void CleanupIdleSessions(object? state)
    {
        _ = Task.Run(CleanupIdleSessionsAsync);
    }

    private async Task CleanupIdleSessionsAsync()
    {
        if (_disposed)
            return;

        await _cleanupSemaphore.WaitAsync();
        try
        {
            var sessionsToRemove = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var kvp in _sessions)
            {
                var session = kvp.Value;
                
                // Remove inactive sessions or sessions that have been idle too long
                if (!session.IsActive || 
                    (now - session.LastActivityUtc).TotalMinutes > IdleTimeoutMinutes)
                {
                    sessionsToRemove.Add(kvp.Key);
                }
            }

            foreach (var sessionId in sessionsToRemove)
            {
                if (_sessions.TryRemove(sessionId, out var session))
                {
                    session.Dispose();
                }
            }
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    /// <summary>
    /// Closes all sessions and disposes the repository.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cleanupTimer.Dispose();

        // Close all active sessions
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _cleanupSemaphore.Dispose();
    }
}

/// <summary>
/// Contains statistics about session repository state.
/// </summary>
public record SessionStatistics
{
    /// <summary>
    /// Total number of sessions.
    /// </summary>
    public int TotalSessions { get; init; }

    /// <summary>
    /// Number of active sessions.
    /// </summary>
    public int ActiveSessions { get; init; }

    /// <summary>
    /// Number of idle sessions.
    /// </summary>
    public int IdleSessions { get; init; }

    /// <summary>
    /// Number of dump analysis sessions.
    /// </summary>
    public int DumpSessions { get; init; }

    /// <summary>
    /// Number of remote debugging sessions.
    /// </summary>
    public int RemoteSessions { get; init; }

    /// <summary>
    /// Maximum concurrent sessions allowed.
    /// </summary>
    public int MaxConcurrentSessions { get; init; }

    /// <summary>
    /// Idle timeout in minutes.
    /// </summary>
    public int IdleTimeoutMinutes { get; init; }
}