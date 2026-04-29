using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using ToolProxy.Services;

namespace ToolProxy.Tools
{
    [McpServerToolType]
    public class LocalTool
    {
        private readonly IMcpDispatcher _dispatcher;
        private readonly IMcpManager _mcpManager;
        private readonly ILogger<LocalTool> _logger;

        public LocalTool(IMcpDispatcher dispatcher, IMcpManager mcpManager, ILogger<LocalTool> logger)
        {
            _dispatcher = dispatcher;
            _mcpManager = mcpManager;
            _logger = logger;
        }

        [McpServerTool, Description("Call a tool on an upstream MCP server fronted by ToolProxy. Use this only for upstream tools advertised by a configured server (see `list_servers`); consult the matching toolproxy-* skill for the exact server, tool, and arguments envelope. The proxy's own meta-tools — `list_servers` and `install_skills` — are top-level tools on this MCP server and must be called directly, not routed through `call_external_tool`.")]
        public async Task<string> CallExternalToolAsync(
            [Description("Name of the upstream MCP server (matches the configured server name)")] string server,
            [Description("Name of the tool to call on that server")] string tool,
            [Description("JSON object of arguments for the tool, matching the upstream tool's input schema")] JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dispatcher.CallExternalToolAsync(server, tool, arguments, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling external tool {Server}.{Tool}", server, tool);
                return $"Error calling external tool {server}.{tool}: {ex.Message}";
            }
        }

        [McpServerTool, Description("List all configured upstream MCP servers fronted by ToolProxy, with each server's description and tool count. This is a top-level proxy tool — call it directly; do not route it through `call_external_tool`. Useful for discovering the exact server names (case is preserved as configured) before dispatching upstream calls; for actual tool usage on a server, refer to the matching toolproxy-* skill.")]
        public async Task<string> ListServersAsync(CancellationToken cancellationToken = default)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            try
            {
                var runningServers = await _mcpManager.GetRunningServersAsync(cancellationToken);

                var servers = runningServers.Select(s => new
                {
                    name = s.Name,
                    description = s.Description,
                    tool_count = s.AvailableToolsWithInfo.Count
                }).ToArray();

                return JsonSerializer.Serialize(new { servers }, jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing servers");
                return JsonSerializer.Serialize(new { error = ex.Message }, jsonOptions);
            }
        }
    }
}
