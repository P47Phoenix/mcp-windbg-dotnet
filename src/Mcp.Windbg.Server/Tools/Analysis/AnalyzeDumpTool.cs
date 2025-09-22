using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Analysis;

/// <summary>
/// Tool for performing comprehensive dump analysis with structured output.
/// </summary>
public sealed class AnalyzeDumpTool : ToolBase<AnalyzeDumpTool.AnalyzeDumpArgs, AnalyzeDumpTool.AnalyzeDumpResult>
{
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Initializes the analyze dump tool with the specified session repository.
    /// </summary>
    /// <param name="sessionRepository">Session repository</param>
    public AnalyzeDumpTool(SessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc/>
    public override string Name => "analyze_dump";

    /// <inheritdoc/>
    public override string Description => "Perform comprehensive dump analysis including crash analysis, stack traces, and system state.";

    /// <inheritdoc/>
    public override async Task<AnalyzeDumpResult> ExecuteTypedAsync(AnalyzeDumpArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.SessionId))
        {
            throw new ArgumentException("sessionId is required");
        }

        var session = _sessionRepository.GetSession(args.SessionId);
        if (session == null)
        {
            return AnalyzeDumpResult.CreateError(args.SessionId, "SessionNotFound", $"Session {args.SessionId} not found");
        }
        if (!session.IsActive)
        {
            return AnalyzeDumpResult.CreateError(args.SessionId, "SessionInactive", $"Session {args.SessionId} is not active");
        }

        try
        {
            var started = DateTime.UtcNow;
            var analysis = await PerformAnalysisAsync(session,
                args.IncludeModules, args.IncludeThreads, args.IncludeRegisters, args.StackFrameCount, ct).ConfigureAwait(false);
            var finished = DateTime.UtcNow;

            return AnalyzeDumpResult.CreateSuccess(args.SessionId,
                (int)(finished - started).TotalMilliseconds,
                finished,
                analysis.JsonData,
                analysis.MarkdownReport,
                new AnalyzeDumpResult.AnalysisOptionsRecord(
                    args.IncludeModules,
                    args.IncludeThreads,
                    args.IncludeRegisters,
                    args.StackFrameCount));
        }
        catch (Exception ex)
        {
            return AnalyzeDumpResult.CreateError(args.SessionId, "AnalysisError", ex.Message);
        }
    }

    private async Task<AnalysisResult> PerformAnalysisAsync(CdbSession session, bool includeModules, bool includeThreads, bool includeRegisters, int stackFrameCount, CancellationToken ct)
    {
        var sections = new List<AnalysisSection>();

        // 1. Last Event
        var lastEventOutput = await session.ExecuteCommandAsync(".lastevent", 30, ct);
        sections.Add(new AnalysisSection("Last Event", lastEventOutput));

        // 2. Crash Analysis
        var analyzeOutput = await session.ExecuteCommandAsync("!analyze -v", 60, ct);
        sections.Add(new AnalysisSection("Crash Analysis", analyzeOutput));

        // 3. Current Stack
        var stackOutput = await session.ExecuteCommandAsync($"kb {stackFrameCount}", 30, ct);
        sections.Add(new AnalysisSection("Current Stack", stackOutput));

        // 4. Thread Information (if requested)
        if (includeThreads)
        {
            var threadsOutput = await session.ExecuteCommandAsync("~", 30, ct);
            sections.Add(new AnalysisSection("Threads", threadsOutput));

            // Get stacks for all threads (abbreviated)
            var allStacksOutput = await session.ExecuteCommandAsync("~*k", 60, ct);
            sections.Add(new AnalysisSection("All Thread Stacks", allStacksOutput));
        }

        // 5. Module Information (if requested)
        if (includeModules)
        {
            var modulesOutput = await session.ExecuteCommandAsync("lmv", 30, ct);
            sections.Add(new AnalysisSection("Loaded Modules", modulesOutput));
        }

        // 6. Registers (if requested)
        if (includeRegisters)
        {
            var registersOutput = await session.ExecuteCommandAsync("r", 30, ct);
            sections.Add(new AnalysisSection("Registers", registersOutput));
        }

        // Generate JSON data
        var jsonData = new JsonObject
        {
            ["sections"] = new JsonArray(sections.Select(s => new JsonObject
            {
                ["title"] = s.Title,
                ["content"] = s.Content,
                ["contentLines"] = s.Content.Split('\n').Length
            }).ToArray()),
            ["sessionInfo"] = new JsonObject
            {
                ["sessionId"] = session.SessionId,
                ["target"] = session.Target,
                ["sessionType"] = session.Type.ToString()
            },
            ["analysisOptions"] = new JsonObject
            {
                ["includeModules"] = includeModules,
                ["includeThreads"] = includeThreads,
                ["includeRegisters"] = includeRegisters,
                ["stackFrameCount"] = stackFrameCount
            }
        };

        // Generate Markdown report
        var markdown = GenerateMarkdownReport(sections, session);

        return new AnalysisResult(jsonData, markdown);
    }

    private static string GenerateMarkdownReport(List<AnalysisSection> sections, CdbSession session)
    {
        var markdown = new StringBuilder();

        // Header
        markdown.AppendLine($"# Crash Dump Analysis Report");
        markdown.AppendLine();
        markdown.AppendLine($"**Session ID:** {session.SessionId}");
        markdown.AppendLine($"**Target:** {session.Target}");
        markdown.AppendLine($"**Session Type:** {session.Type}");
        markdown.AppendLine($"**Analysis Time:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        markdown.AppendLine();

        // Table of Contents
        markdown.AppendLine("## Table of Contents");
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var anchor = section.Title.ToLowerInvariant().Replace(' ', '-');
            markdown.AppendLine($"{i + 1}. [{section.Title}](#{anchor})");
        }
        markdown.AppendLine();

        // Sections
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var anchor = section.Title.ToLowerInvariant().Replace(' ', '-');
            
            markdown.AppendLine($"## {i + 1}. {section.Title} {{#{anchor}}}");
            markdown.AppendLine();
            markdown.AppendLine("```");
            markdown.AppendLine(section.Content);
            markdown.AppendLine("```");
            markdown.AppendLine();
        }

        // Footer
        markdown.AppendLine("---");
        markdown.AppendLine($"*Generated by mcp-windbg-dotnet at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

        return markdown.ToString();
    }

    private record AnalysisSection(string Title, string Content);
    private record AnalysisResult(JsonObject JsonData, string MarkdownReport);

    // Typed contracts
    public sealed class AnalyzeDumpArgs
    {
        [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
        [JsonPropertyName("includeModules")] public bool IncludeModules { get; set; } = true;
        [JsonPropertyName("includeThreads")] public bool IncludeThreads { get; set; } = true;
        [JsonPropertyName("includeRegisters")] public bool IncludeRegisters { get; set; } = false;
        [JsonPropertyName("stackFrameCount")] public int StackFrameCount { get; set; } = 10;
    }

    public sealed class AnalyzeDumpResult
    {
        [JsonPropertyName("success")] public bool Success { get; init; }
        [JsonPropertyName("sessionId")] public string SessionId { get; init; } = string.Empty;
        [JsonPropertyName("analysisTimeMs")] public int? AnalysisTimeMs { get; init; }
        [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
        [JsonPropertyName("analysis")] public JsonObject? Analysis { get; init; }
        [JsonPropertyName("markdown")] public string? Markdown { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
        [JsonPropertyName("options")] public AnalysisOptionsRecord? Options { get; init; }

        public sealed record AnalysisOptionsRecord(
            [property: JsonPropertyName("includeModules")] bool IncludeModules,
            [property: JsonPropertyName("includeThreads")] bool IncludeThreads,
            [property: JsonPropertyName("includeRegisters")] bool IncludeRegisters,
            [property: JsonPropertyName("stackFrameCount")] int StackFrameCount);

        public static AnalyzeDumpResult CreateSuccess(string sessionId, int analysisTimeMs, DateTime finishedUtc, JsonObject analysis, string markdown, AnalysisOptionsRecord options)
            => new()
            {
                Success = true,
                SessionId = sessionId,
                AnalysisTimeMs = analysisTimeMs,
                Timestamp = finishedUtc.ToString("O"),
                Analysis = analysis,
                Markdown = markdown,
                Options = options
            };

        public static AnalyzeDumpResult CreateError(string sessionId, string error, string message)
            => new()
            {
                Success = false,
                SessionId = sessionId,
                Error = error,
                Message = message
            };
    }
}