using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using ToolProxy.Services;

namespace ToolProxy.Tools
{
    [McpServerToolType]
    public class EnhancedLocalTool
    {
        private readonly IToolIndexService _toolIndexService;
        private readonly ILogger<EnhancedLocalTool> _logger;

        public EnhancedLocalTool(IToolIndexService toolIndexService, ILogger<EnhancedLocalTool> logger)
        {
            _toolIndexService = toolIndexService;
            _logger = logger;
        }

        [McpServerTool, Description("Search for tools using semantic similarity (requires Semantic Kernel implementation)")]
        public async Task<string> SearchToolsSemanticAsync(
            [Description("Natural language description of the functionality you're looking for")] string query,
            [Description("Maximum number of results to return (default: 5)")] int maxResults = 5,
            [Description("Minimum relevance score between 0.0 and 1.0 (default: 0.3)")] float minRelevanceScore = 0.3f,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var results = await _toolIndexService.SearchToolsSemanticAsync(query, maxResults, minRelevanceScore);

                if (!results.Any())
                {
                    return $"No tools found with semantic similarity to '{query}' (min score: {minRelevanceScore})";
                }

                var resultLines = new List<string>
                {
                    $"Found {results.Count} semantically similar tools for '{query}':",
                    ""
                };

                foreach (var result in results)
                {
                    resultLines.Add($"?? {result.ServerName}.{result.Tool.Name} (Score: {result.RelevanceScore:F3})");
                    resultLines.Add($"   ?? {result.Tool.Description}");

                    if (result.Tool.Parameters.Any())
                    {
                        var paramNames = string.Join(", ", result.Tool.Parameters.Select(p => p.Name));
                        resultLines.Add($"   ??  Parameters: {paramNames}");
                    }

                    resultLines.Add("");
                }

                return string.Join("\n", resultLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search for query: {Query}", query);
                return $"Error performing semantic search: {ex.Message}";
            }
        }

        [McpServerTool, Description("Get detailed information about tool indexing service")]
        public async Task<string> GetToolIndexInfoAsync(CancellationToken cancellationToken = default)
        {
            var serviceType = _toolIndexService.GetType().Name;
            var isReady = _toolIndexService.IsIndexReady;
            var allTools = await _toolIndexService.GetAllExternalToolsAsync();

            var totalTools = allTools.Values.Sum(tools => tools.Count);
            var serverCount = allTools.Count;

            var info = new List<string>
            {
                $"Tool Index Service: {serviceType}",
                $"Index Ready: {isReady}",
                $"Total Servers: {serverCount}",
                $"Total Tools: {totalTools}",
                ""
            };

            if (serviceType.Contains("SemanticKernel"))
            {
                info.Add("?? Semantic search capabilities enabled");
                info.Add("   • Vector-based similarity search");
                info.Add("   • Natural language queries");
                info.Add("   • Relevance scoring");
            }
            else
            {
                info.Add("?? Basic text-based search only");
                info.Add("   • Use --semantic-kernel flag to enable AI-powered search");
            }

            info.Add("");
            info.Add("Available servers:");

            foreach (var (serverName, tools) in allTools)
            {
                info.Add($"  • {serverName}: {tools.Count} tools");
            }

            return string.Join("\n", info);
        }

        [McpServerTool, Description("Call a tool from an external MCP server")]
        public async Task<string> CallExternalToolAsync(
            [Description("Name of the MCP server")] string serverName,
            [Description("Name of the tool to call")] string toolName,
            [Description("JSON parameters for the tool")] JsonElement parameters,
            CancellationToken cancellationToken = default)
        {
            return await _toolIndexService.CallExternalToolAsync(serverName, toolName, parameters, cancellationToken);
        }

        [McpServerTool, Description("Refresh the tool index to pick up any new or updated tools")]
        public async Task<string> RefreshToolIndexAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _toolIndexService.RefreshIndexAsync();
                var allTools = await _toolIndexService.GetAllExternalToolsAsync();
                var totalTools = allTools.Values.Sum(tools => tools.Count);

                return $"Tool index refreshed successfully. Found {allTools.Count} servers with {totalTools} total tools.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing tool index");
                return $"Error refreshing tool index: {ex.Message}";
            }
        }
    }
}