using System.Text.Json;

namespace ToolProxy.Services
{
    /// <summary>
    /// Represents a tool search result with relevance score.
    /// </summary>
    public record ToolSearchResult(string ServerName, ToolInfo Tool, float RelevanceScore);

    public interface IToolIndexService
    {
        // External MCP server tools
        IReadOnlyDictionary<string, IReadOnlyList<ToolInfo>> GetAllExternalToolsAsync();
        IReadOnlyList<ToolInfo>? GetServerToolsAsync(string serverName);

        // Semantic search capabilities (only available in SemanticKernelToolIndexService)
        Task<IReadOnlyList<ToolSearchResult>> SearchToolsSemanticAsync(
            string query,
            int maxResults = 5,
            float minRelevanceScore = 0.6f)
        {
            // Default implementation for backward compatibility
            return Task.FromResult<IReadOnlyList<ToolSearchResult>>(Array.Empty<ToolSearchResult>());
        }

        // Tool execution
        Task<string> CallExternalToolAsync(string serverName, string toolName, JsonElement parameters, CancellationToken cancellationToken = default);

        // Index management
        Task RefreshIndexAsync();
    }
}