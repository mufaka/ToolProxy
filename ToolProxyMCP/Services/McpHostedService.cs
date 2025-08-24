using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ToolProxy.Services
{
    public class McpHostedService : IHostedService
    {
        private readonly IMcpManager _mcpManager;
        private readonly IToolIndexService _toolIndexService;
        private readonly ILogger<McpHostedService> _logger;

        public McpHostedService(
            IMcpManager mcpManager,
            IToolIndexService toolIndexService,
            ILogger<McpHostedService> logger)
        {
            _mcpManager = mcpManager ?? throw new ArgumentNullException(nameof(mcpManager));
            _toolIndexService = toolIndexService ?? throw new ArgumentNullException(nameof(toolIndexService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting MCP Hosted Service...");

            var success = await _mcpManager.StartAllServersAsync(cancellationToken);

            if (success)
            {
                _logger.LogInformation("All enabled MCP servers started successfully");

                // Build the tool index after servers are started
                await _toolIndexService.RefreshIndexAsync();
            }
            else
            {
                _logger.LogWarning("Some MCP servers failed to start");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping MCP Hosted Service...");

            var success = await _mcpManager.StopAllServersAsync(cancellationToken);

            if (success)
            {
                _logger.LogInformation("All MCP servers stopped successfully");
            }
            else
            {
                _logger.LogWarning("Some MCP servers failed to stop gracefully");
            }
        }
    }
}