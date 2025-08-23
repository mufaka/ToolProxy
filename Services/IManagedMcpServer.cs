using System.Text.Json;

namespace ToolProxy.Services
{
    public interface IManagedMcpServer
    {
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; }
        IReadOnlyList<string> AvailableTools { get; }
        IReadOnlyList<ToolInfo> AvailableToolsWithInfo { get; }
        Task<bool> StartAsync(CancellationToken cancellationToken = default);
        Task<bool> StopAsync(CancellationToken cancellationToken = default);
        Task<string> CallToolAsync(string toolName, JsonElement parameters, CancellationToken cancellationToken = default);
        Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);
        Task RefreshToolsAsync(CancellationToken cancellationToken = default);
    }
}
