using System.Text;
using System.Text.Json.Nodes;
using Mcp.Windbg.Server.Session;

namespace Mcp.Windbg.Server.Tools.Analysis;

/// <summary>
/// Tool for performing comprehensive dump analysis with structured output.
/// </summary>
public sealed class AnalyzeDumpTool : ITool
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
    public string Name => "analyze_dump";

    /// <inheritdoc/>
    public string Description => "Perform comprehensive dump analysis including crash analysis, stack traces, and system state.";

    /// <inheritdoc/>
    public async Task<JsonNode> ExecuteAsync(JsonNode? args, CancellationToken ct)
    {
        // Parse arguments
        if (args == null)
            throw new ArgumentException("Missing arguments. Required: sessionId");

        var sessionId = args["sessionId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Missing or empty sessionId argument");

        var includeModules = args["includeModules"]?.GetValue<bool>() ?? true;
        var includeThreads = args["includeThreads"]?.GetValue<bool>() ?? true;
        var includeRegisters = args["includeRegisters"]?.GetValue<bool>() ?? false;
        var stackFrameCount = args["stackFrameCount"]?.GetValue<int>() ?? 10;

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

            var startTime = DateTime.UtcNow;
            var analysis = await PerformAnalysisAsync(session, includeModules, includeThreads, includeRegisters, stackFrameCount, ct);
            var endTime = DateTime.UtcNow;

            var result = new JsonObject
            {
                ["success"] = true,
                ["sessionId"] = sessionId,
                ["analysisTimeMs"] = (int)(endTime - startTime).TotalMilliseconds,
                ["timestamp"] = endTime.ToString("O"),
                ["analysis"] = analysis.JsonData,
                ["markdown"] = analysis.MarkdownReport
            };

            return result;
        }
        catch (Exception ex)
        {
            return new JsonObject
            {
                ["success"] = false,
                ["error"] = "AnalysisError",
                ["message"] = ex.Message,
                ["sessionId"] = sessionId
            };
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
}