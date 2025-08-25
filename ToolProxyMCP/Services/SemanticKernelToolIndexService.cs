using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.InMemory;
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
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly SemanticKernelSettings _settings;
        private readonly ILogger<SemanticKernelToolIndexService> _logger;
        private readonly InMemoryVectorStore _vectorStore;

        // Cache for fast tool lookup
        private readonly ConcurrentDictionary<string, IReadOnlyList<ToolInfo>> _toolCache = new();

        public SemanticKernelToolIndexService(
            IMcpManager mcpManager,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            SemanticKernelSettings settings,
            ILogger<SemanticKernelToolIndexService> logger)
        {
            _mcpManager = mcpManager ?? throw new ArgumentNullException(nameof(mcpManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
            _vectorStore = new InMemoryVectorStore();
        }


        public IReadOnlyDictionary<string, IReadOnlyList<ToolInfo>> GetAllExternalToolsAsync()
        {
            return _toolCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IReadOnlyList<ToolInfo>? GetServerToolsAsync(string serverName)
        {
            return _toolCache.TryGetValue(serverName, out var tools) ? tools : null;
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
            float minRelevanceScore = 0.55f)
        {
            try
            {
                _logger.LogDebug("Performing semantic search for query: {Query}", query);

                // Generate embedding for the search query
                var queryEmbedding = await _embeddingGenerator.GenerateAsync(query);
                var collection = _vectorStore.GetCollection<string, ToolVectorRecord>("tool_index");
                var vectorStoreResults = collection.SearchAsync(queryEmbedding, maxResults);

                var results = new List<ToolSearchResult>();

                foreach (var result in await vectorStoreResults.ToListAsync())
                {
                    _logger.LogDebug("Vector store search result: {Id} with score {Score}",
                        result.Record.Id, result.Score);

                    if (result.Score >= minRelevanceScore)
                    {
                        _logger.LogDebug("Result {Id} passed min relevance score {MinScore}",
                            result.Record.Id, minRelevanceScore);

                        var toolInfo = new ToolInfo(
                            result.Record.ToolName,
                            result.Record.Description,
                            ParseParametersFromJson(result.Record.ParametersJson));

                        results.Add(new ToolSearchResult(
                            result.Record.ServerName,
                            toolInfo,
                            (float)result.Score));
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
            try
            {
                _logger.LogInformation("Refreshing semantic tool index...");

                // Embedding type is not supported by in memory vector store.
                var collection = _vectorStore.GetCollection<string, ToolVectorRecord>("tool_index");
                await collection.EnsureCollectionExistsAsync();

                var runningServers = await _mcpManager.GetRunningServersAsync();
                var newCache = new ConcurrentDictionary<string, IReadOnlyList<ToolInfo>>();
                var vectorRecords = new List<ToolVectorRecord>();

                foreach (var server in runningServers)
                {
                    var tools = server.AvailableToolsWithInfo;
                    newCache[server.Name] = tools;

                    // Create vector records for each tool
                    foreach (var tool in tools)
                    {
                        var record = await CreateToolVectorRecordAsync(server.Name, server.Description, tool);
                        vectorRecords.Add(record);
                    }
                }

                // upsert all vector records
                await collection.UpsertAsync(vectorRecords);

                // Update caches atomically
                _toolCache.Clear();

                foreach (var kvp in newCache)
                {
                    _toolCache[kvp.Key] = kvp.Value;
                }

                var totalTools = _toolCache.Values.Sum(tools => tools.Count);
                _logger.LogInformation("Semantic tool index refreshed: {ServerCount} servers, {ToolCount} total tools, {VectorRecords} vector records",
                    _toolCache.Count, totalTools, vectorRecords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing semantic tool index");
                throw;
            }
            finally
            {
            }
        }

        private async Task<ToolVectorRecord> CreateToolVectorRecordAsync(string serverName, string serverDescription, ToolInfo tool)
        {
            var parametersJson = JsonSerializer.Serialize(tool.Parameters);
            var parameterNames = string.Join(", ", tool.Parameters.Select(p => p.Name));
            var parameterDescriptions = string.Join("; ", tool.Parameters.Select(p => $"{p.Name}: {p.Description}"));

            var embeddingInput = $"The {serverName} described as \"{serverDescription}\" has a tool named {tool.Name} that can be used for: {tool.Description}. The following parameters can be used when calling the tool:";

            if (tool.Parameters.Count > 0)
            {
                embeddingInput += $"{parameterDescriptions}.";
            }

            // Use the server name, tool name and description to generate the embedding. 
            var embedding = await _embeddingGenerator.GenerateAsync(embeddingInput);

            return new ToolVectorRecord
            {
                Id = ToolVectorRecord.CreateId(serverName, tool.Name),
                ServerName = serverName,
                ToolName = tool.Name,
                Description = tool.Description,
                ParametersJson = parametersJson,
                ParameterCount = tool.Parameters.Count,
                ParameterNames = parameterNames,
                Embedding = embedding.Vector,
                LastUpdated = DateTime.Now
            };
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