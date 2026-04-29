using System.Text.Json;

namespace ToolProxy.Services
{
    public interface IMcpDispatcher
    {
        Task<string> CallExternalToolAsync(
            string server,
            string tool,
            JsonElement arguments,
            CancellationToken cancellationToken = default);
    }
}
