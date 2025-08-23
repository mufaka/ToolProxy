namespace ToolProxy.Services
{
    public interface IMcpManager
    {
        Task<bool> StartAllServersAsync(CancellationToken cancellationToken = default);
        Task<bool> StopAllServersAsync(CancellationToken cancellationToken = default);
        Task<IManagedMcpServer?> GetServerAsync(string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<IManagedMcpServer>> GetRunningServersAsync(CancellationToken cancellationToken = default);
        Task RefreshAllToolsAsync(CancellationToken cancellationToken = default);
    }
}
