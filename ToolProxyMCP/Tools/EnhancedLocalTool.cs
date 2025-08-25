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
                    resultLines.Add($"{result.ServerName}.{result.Tool.Name} (Score: {result.RelevanceScore:F3})");
                    resultLines.Add($"    {result.Tool.Description}");

                    if (result.Tool.Parameters.Any())
                    {
                        var paramNames = string.Join(", ", result.Tool.Parameters.Select(p => p.Name));
                        resultLines.Add($"    Parameters: {paramNames}");

                        // Add detailed parameter information
                        resultLines.Add($"    Parameter details:");
                        foreach (var param in result.Tool.Parameters)
                        {
                            var required = param.IsRequired ? " (required)" : " (optional)";
                            resultLines.Add($"      • {param.Name} ({param.Type}){required}: {param.Description}");
                        }
                    }

                    // Generate exact JSON-RPC call structure for LLMs
                    resultLines.Add($"    ");
                    resultLines.Add($"    EXACT TOOL CALL - Use this JSON-RPC structure:");
                    resultLines.Add($"    {{");
                    resultLines.Add($"      \"jsonrpc\": \"2.0\",");
                    resultLines.Add($"      \"id\": \"unique-id\",");
                    resultLines.Add($"      \"method\": \"tools/call\",");
                    resultLines.Add($"      \"params\": {{");
                    //resultLines.Add($"        \"name\": \"tool_proxy_call_external_tool\",");
                    resultLines.Add($"        \"name\": \"call_external_tool\",");
                    resultLines.Add($"        \"arguments\": {{");
                    resultLines.Add($"          \"serverName\": \"{result.ServerName}\",");
                    resultLines.Add($"          \"toolName\": \"{result.Tool.Name}\",");

                    if (result.Tool.Parameters.Any())
                    {
                        resultLines.Add($"          \"parameters\": {{");

                        var parameterExamples = result.Tool.Parameters.Select(p =>
                        {
                            var example = GetParameterExample(p.Type, p.Description);
                            return $"            \"{p.Name}\": {example}";
                        });

                        resultLines.Add(string.Join(",\n", parameterExamples));
                        resultLines.Add($"          }}");
                    }
                    else
                    {
                        resultLines.Add($"          \"parameters\": {{}}");
                    }

                    resultLines.Add($"        }}");
                    resultLines.Add($"      }}");
                    resultLines.Add($"    }}");
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

        private static string GetParameterExample(string type, string description)
        {
            return type.ToLowerInvariant() switch
            {
                "string" => $"\"<{description.ToLowerInvariant().Replace(" ", "_")}>\"",
                "int" or "integer" => "0",
                "float" or "double" or "number" => "0.0",
                "bool" or "boolean" => "false",
                "array" => "[]",
                "object" => "{}",
                _ when type.Contains("[]") => "[]",
                _ when type.Contains("object") || type.Contains("Object") => "{}",
                _ => $"\"<{type.ToLowerInvariant()}>\""
            };
        }

        [McpServerTool, Description("Get detailed information about tool indexing service")]
        public async Task<string> GetToolIndexInfoAsync(CancellationToken cancellationToken = default)
        {
            var serviceType = _toolIndexService.GetType().Name;
            var allTools = _toolIndexService.GetAllExternalToolsAsync();

            var totalTools = allTools.Values.Sum(tools => tools.Count);
            var serverCount = allTools.Count;

            var info = new List<string>
            {
                $"Tool Index Service: {serviceType}",
                $"Total Servers: {serverCount}",
                $"Total Tools: {totalTools}",
                ""
            };

            info.Add("Semantic search capabilities enabled");
            info.Add("   • Vector-based similarity search");
            info.Add("   • Natural language queries");
            info.Add("   • Relevance scoring");

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
                var allTools = _toolIndexService.GetAllExternalToolsAsync();
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