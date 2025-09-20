using System.Text.Json.Nodes;

namespace Mcp.Windbg.Server.Tools.Session;

/// <summary>
/// Tool for listing available dump files in specified directories.
/// </summary>
public sealed class ListDumpsTool : ITool
{
    private readonly string[] _searchPaths;

    /// <summary>
    /// Initializes the list dumps tool with default search paths.
    /// </summary>
    public ListDumpsTool() : this(GetDefaultSearchPaths()) { }

    /// <summary>
    /// Initializes the list dumps tool with specified search paths.
    /// </summary>
    /// <param name="searchPaths">Directories to search for dump files</param>
    public ListDumpsTool(string[] searchPaths)
    {
        _searchPaths = searchPaths ?? throw new ArgumentNullException(nameof(searchPaths));
    }

    /// <inheritdoc/>
    public string Name => "list_dumps";

    /// <inheritdoc/>
    public string Description => "List available dump files (.dmp, .mdmp) with metadata in configured directories.";

    /// <inheritdoc/>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        var maxResults = args?["maxResults"]?.GetValue<int>() ?? 100;
        var pattern = args?["pattern"]?.GetValue<string>() ?? "*";
        var includeSubdirectories = args?["includeSubdirectories"]?.GetValue<bool>() ?? false;

        try
        {
            var dumps = new List<DumpFileInfo>();

            foreach (var searchPath in _searchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                await FindDumpsInDirectoryAsync(searchPath, pattern, includeSubdirectories, dumps, maxResults, ct);

                if (dumps.Count >= maxResults)
                    break;
            }

            // Sort by modification time (newest first)
            dumps.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

            var result = new JsonObject
            {
                ["success"] = true,
                ["searchPaths"] = JsonValue.Create(_searchPaths),
                ["totalFound"] = dumps.Count,
                ["maxResults"] = maxResults,
                ["dumps"] = new JsonArray(dumps.Select(CreateDumpJson).ToArray())
            };

            return result;
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "ListDumpsError",
                ["message"] = ex.Message
            };
        }
    }

    private static async Task FindDumpsInDirectoryAsync(string directory, string pattern, bool includeSubdirectories, List<DumpFileInfo> dumps, int maxResults, CancellationToken ct)
    {
        var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var extensions = new[] { "*.dmp", "*.mdmp" };

        foreach (var extension in extensions)
        {
            var searchPattern = pattern == "*" ? extension : $"{pattern}.{extension.TrimStart('*', '.')}";
            
            try
            {
                var files = Directory.EnumerateFiles(directory, searchPattern, searchOption);
                
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        dumps.Add(new DumpFileInfo
                        {
                            Path = file,
                            Name = fileInfo.Name,
                            SizeBytes = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            Directory = fileInfo.DirectoryName ?? ""
                        });

                        if (dumps.Count >= maxResults)
                            return;
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
            }
            catch
            {
                // Skip directories we can't access
            }
        }
    }

    private static JsonObject CreateDumpJson(DumpFileInfo dump)
    {
        return new JsonObject
        {
            ["path"] = dump.Path,
            ["name"] = dump.Name,
            ["directory"] = dump.Directory,
            ["sizeBytes"] = dump.SizeBytes,
            ["sizeMB"] = Math.Round(dump.SizeBytes / 1024.0 / 1024.0, 2),
            ["lastModified"] = dump.LastModified.ToString("O"),
            ["lastModifiedRelative"] = GetRelativeTime(dump.LastModified)
        };
    }

    private static string GetRelativeTime(DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;
        
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays} days ago";
        
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours} hours ago";
            
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes} minutes ago";
            
        return "Just now";
    }

    private static string[] GetDefaultSearchPaths()
    {
        var paths = new List<string>();

        // Common Windows dump locations
        var commonPaths = new[]
        {
            @"C:\Windows\Minidump",
            @"C:\Windows\memory.dmp",
            @"C:\CrashDumps",
            @"C:\Dumps",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        foreach (var path in commonPaths)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                paths.Add(path);
            }
        }

        // Add current directory
        paths.Add(Directory.GetCurrentDirectory());

        return paths.ToArray();
    }

    private record DumpFileInfo
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public string Directory { get; init; } = "";
        public long SizeBytes { get; init; }
        public DateTime LastModified { get; init; }
    }
}