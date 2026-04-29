using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ToolProxy.Services
{
    public class McpDispatcher : IMcpDispatcher
    {
        private readonly IMcpManager _mcpManager;
        private readonly ILogger<McpDispatcher> _logger;

        public McpDispatcher(IMcpManager mcpManager, ILogger<McpDispatcher> logger)
        {
            _mcpManager = mcpManager ?? throw new ArgumentNullException(nameof(mcpManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> CallExternalToolAsync(
            string server,
            string tool,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            var managedServer = await _mcpManager.GetServerAsync(server, cancellationToken);
            if (managedServer == null)
            {
                throw new InvalidOperationException(
                    $"Server '{server}' is not configured or not running.");
            }

            return await managedServer.CallToolAsync(tool, arguments, cancellationToken);
        }
    }
}
