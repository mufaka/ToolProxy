using Microsoft.Extensions.Logging;
using ToolProxy.Configuration;

namespace ToolProxy.Services
{
    public class McpManager : IMcpManager, IDisposable
    {
        private readonly AppSettings _settings;
        private readonly ILogger<McpManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<string, IManagedMcpServer> _servers = new();
        private bool _disposed;

        public McpManager(AppSettings settings, ILogger<McpManager> logger, ILoggerFactory loggerFactory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            InitializeServers();
        }

        private void InitializeServers()
        {
            foreach (var config in _settings.McpServers)
            {
                var serverLogger = _loggerFactory.CreateLogger<ManagedMcpServer>();
                var server = new ManagedMcpServer(config, serverLogger);
                _servers[config.Name] = server;
            }
        }

        public async Task<bool> StartAllServersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting all MCP servers...");

            var tasks = _servers.Values
                .Where(s => s.IsEnabled)
                .Select(s => s.StartAsync(cancellationToken));

            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);
            var totalEnabled = _servers.Values.Count(s => s.IsEnabled);

            _logger.LogInformation("Started {SuccessCount}/{TotalCount} MCP servers", successCount, totalEnabled);

            return successCount == totalEnabled;
        }

        public async Task<bool> StopAllServersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Stopping all MCP servers...");

            var tasks = _servers.Values.Select(s => s.StopAsync(cancellationToken));
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r);

            _logger.LogInformation("Stopped {SuccessCount}/{TotalCount} MCP servers", successCount, _servers.Count);

            return successCount == _servers.Count;
        }

        public async Task<IManagedMcpServer?> GetServerAsync(string name, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_servers.TryGetValue(name, out var server) ? server : null);
        }

        public async Task<IReadOnlyList<IManagedMcpServer>> GetRunningServersAsync(CancellationToken cancellationToken = default)
        {
            var runningServers = new List<IManagedMcpServer>();

            foreach (var server in _servers.Values)
            {
                if (await server.IsRunningAsync(cancellationToken))
                {
                    runningServers.Add(server);
                }
            }

            return runningServers.AsReadOnly();
        }

        public async Task RefreshAllToolsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Refreshing tools for all running MCP servers...");

            var runningServers = await GetRunningServersAsync(cancellationToken);
            var tasks = runningServers.Select(s => s.RefreshToolsAsync(cancellationToken));

            await Task.WhenAll(tasks);

            _logger.LogInformation("Completed tools refresh for all servers");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var server in _servers.Values)
                {
                    if (server is IDisposable disposableServer)
                    {
                        disposableServer.Dispose();
                    }
                }
                _servers.Clear();
                _disposed = true;
            }
        }
    }
}
