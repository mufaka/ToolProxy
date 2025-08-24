using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Embeddings;
using System.Collections.Concurrent;
using System.Text.Json;
using ToolProxy.Configuration;
using ToolProxy.Models;

namespace ToolProxy.Services
{
    /// <summary>
    /// Semantic Kernel-based implementation of IToolIndexService that uses vector embeddings 
    /// for intelligent tool search and discovery.
    /// </summary>
    public class SemanticKernelToolIndexService : IToolIndexService
    {
        private readonly IMcpManager _mcpManager;
        private readonly ITextEmbeddingGenerationService _embeddingService;
        private readonly SemanticKernelSettings _settings;
        private readonly ILogger<SemanticKernelToolIndexService> _logger;
        
        // Cache for fast tool lookup
        private readonly ConcurrentDictionary<string, IReadOnlyList<ToolInfo>> _toolCache = new();
        private readonly ConcurrentDictionary<string, ToolVectorRecord> _vectorRecordCache = new();
        private volatile bool _isIndexReady;
        private readonly SemaphoreSlim _indexingSemaphore = new(1, 1);

        public SemanticKernelToolIndexService(
            IMcpManager mcpManager,
            ITextEmbeddingGenerationService embeddingService,
            SemanticKernelSettings settings,
            ILogger<SemanticKernelToolIndexService> logger)
        {
            _mcpManager = mcpManager ?? throw new ArgumentNullException(nameof(mcpManager));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsIndexReady => _isIndexReady;

        public async Task<IReadOnlyDictionary<string, IReadOnlyList<ToolInfo>>> GetAllExternalToolsAsync()
        {
            if (!_isIndexReady)
            {
                await RefreshIndexAsync();
            }

            return _toolCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public async Task<IReadOnlyList<ToolInfo>?> GetServerToolsAsync(string serverName)
        {
            if (!_isIndexReady)
            {
                await RefreshIndexAsync();
            }

            return _toolCache.TryGetValue(serverName, out var tools) ? tools : null;
        }

        public async Task<ToolInfo?> FindToolAsync(string toolName, string? preferredServer = null)
        {
            if (!_isIndexReady)
            {
                await RefreshIndexAsync();
            }

            // First try preferred server if specified
            if (!string.IsNullOrEmpty(preferredServer) &&
                _toolCache.TryGetValue(preferredServer, out var preferredTools))
            {
                var tool = preferredTools.FirstOrDefault(t =>
                    t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                if (tool != null) return tool;
            }

            // Search all servers
            foreach (var serverTools in _toolCache.Values)
            {
                var tool = serverTools.FirstOrDefault(t =>
                    t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                if (tool != null) return tool;
            }

            return null;
        }

        /// <summary>
        /// Performs semantic search across all tools using vector similarity.
        /// </summary>
        /// <param name="query">Natural language query describing the desired tool functionality</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <param name="minRelevanceScore">Minimum relevance score (0.0 to 1.0)</param>
        /// <returns>List of tools ranked by semantic similarity</returns>
        public async Task<IReadOnlyList<ToolSearchResult>> SearchToolsSemanticAsync(
            string query,
            int maxResults = 5,
            float minRelevanceScore = 0.3f)
        {
            if (!_isIndexReady)
            {
                await RefreshIndexAsync();
            }

            try
            {
                _logger.LogDebug("Performing semantic search for query: {Query}", query);

                // Generate embedding for the search query
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

                // Perform in-memory vector similarity search
                var results = new List<ToolSearchResult>();

                foreach (var record in _vectorRecordCache.Values)
                {
                    var similarity = CalculateCosineSimilarity(queryEmbedding.Span, record.Embedding.Span);
                    
                    if (similarity >= minRelevanceScore)
                    {
                        var toolInfo = new ToolInfo(
                            record.ToolName,
                            record.Description,
                            ParseParametersFromJson(record.ParametersJson));

                        results.Add(new ToolSearchResult(
                            record.ServerName,
                            toolInfo,
                            similarity));
                    }
                }

                // Sort by similarity score (highest first) and take top results
                var sortedResults = results
                    .OrderByDescending(r => r.RelevanceScore)
                    .Take(maxResults)
                    .ToList();

                _logger.LogInformation("Semantic search returned {Count} results for query: {Query}",
                    sortedResults.Count, query);

                return sortedResults.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing semantic search for query: {Query}", query);
                throw;
            }
        }

        public async Task<string> CallExternalToolAsync(string serverName, string toolName, JsonElement parameters, CancellationToken cancellationToken = default)
        {
            var server = await _mcpManager.GetServerAsync(serverName, cancellationToken);
            if (server == null)
            {
                throw new InvalidOperationException($"Server '{serverName}' not found or not running");
            }

            return await server.CallToolAsync(toolName, parameters, cancellationToken);
        }

        public async Task RefreshIndexAsync()
        {
            await _indexingSemaphore.WaitAsync();
            try
            {
                _logger.LogInformation("Refreshing semantic tool index...");

                var runningServers = await _mcpManager.GetRunningServersAsync();
                var newCache = new ConcurrentDictionary<string, IReadOnlyList<ToolInfo>>();
                var newVectorRecordCache = new ConcurrentDictionary<string, ToolVectorRecord>();

                foreach (var server in runningServers)
                {
                    var tools = server.AvailableToolsWithInfo;
                    newCache[server.Name] = tools;

                    // Create vector records for each tool
                    foreach (var tool in tools)
                    {
                        var record = await CreateToolVectorRecordAsync(server.Name, tool);
                        newVectorRecordCache[record.Id] = record;
                    }
                }

                // Update caches atomically
                _toolCache.Clear();
                _vectorRecordCache.Clear();

                foreach (var kvp in newCache)
                {
                    _toolCache[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in newVectorRecordCache)
                {
                    _vectorRecordCache[kvp.Key] = kvp.Value;
                }

                _isIndexReady = true;

                var totalTools = _toolCache.Values.Sum(tools => tools.Count);
                _logger.LogInformation("Semantic tool index refreshed: {ServerCount} servers, {ToolCount} total tools, {VectorRecords} vector records",
                    _toolCache.Count, totalTools, newVectorRecordCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing semantic tool index");
                throw;
            }
            finally
            {
                _indexingSemaphore.Release();
            }
        }

        private async Task<ToolVectorRecord> CreateToolVectorRecordAsync(string serverName, ToolInfo tool)
        {
            var searchableText = ToolVectorRecord.CreateSearchableText(tool.Name, tool.Description);
            var parametersJson = JsonSerializer.Serialize(tool.Parameters);
            var parameterNames = string.Join(", ", tool.Parameters.Select(p => p.Name));

            // Generate embedding for the searchable text
            var embedding = await _embeddingService.GenerateEmbeddingAsync(searchableText);

            return new ToolVectorRecord
            {
                Id = ToolVectorRecord.CreateId(serverName, tool.Name),
                ServerName = serverName,
                ToolName = tool.Name,
                Description = tool.Description,
                SearchableText = searchableText,
                ParametersJson = parametersJson,
                ParameterCount = tool.Parameters.Count,
                ParameterNames = parameterNames,
                Embedding = embedding,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Calculates cosine similarity between two vectors.
        /// </summary>
        /// <param name="vector1">First vector</param>
        /// <param name="vector2">Second vector</param>
        /// <returns>Cosine similarity score between 0 and 1</returns>
        private static float CalculateCosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            if (vector1.Length == 0)
            {
                return 0.0f;
            }

            float dotProduct = 0.0f;
            float magnitude1 = 0.0f;
            float magnitude2 = 0.0f;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = MathF.Sqrt(magnitude1);
            magnitude2 = MathF.Sqrt(magnitude2);

            if (magnitude1 == 0.0f || magnitude2 == 0.0f)
            {
                return 0.0f;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }

        private static IReadOnlyList<ToolParameter> ParseParametersFromJson(string parametersJson)
        {
            try
            {
                if (string.IsNullOrEmpty(parametersJson))
                    return Array.Empty<ToolParameter>();

                var parameters = JsonSerializer.Deserialize<ToolParameter[]>(parametersJson);
                return parameters?.ToList().AsReadOnly() ?? (IReadOnlyList<ToolParameter>)Array.Empty<ToolParameter>();
            }
            catch
            {
                return Array.Empty<ToolParameter>();
            }
        }
    }
}