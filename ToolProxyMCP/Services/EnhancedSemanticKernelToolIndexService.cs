using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Ollama;
using System.Collections.Concurrent;
using System.Text.Json;
using ToolProxy.Configuration;
using ToolProxy.Models;

namespace ToolProxy.Services
{
    /// <summary>
    /// Enhanced Semantic Kernel-based implementation of IToolIndexService that uses LLM-generated phrases 
    /// for improved semantic search accuracy. 
    /// 
    /// This service improves upon the base SemanticKernelToolIndexService by using chat completions
    /// to generate optimized search phrases before embedding. This approach can result in better 
    /// semantic matching because the LLM can understand the tool's purpose and generate phrases 
    /// that capture common use cases and alternative terminology.
    /// 
    /// Key differences from SemanticKernelToolIndexService:
    /// 1. Uses IChatCompletionService to generate embedding phrases via LLM
    /// 2. Generates all phrases first, then embeddings (avoids model switching overhead)
    /// 3. Has fallback mechanisms for phrase generation failures
    /// 4. Provides more comprehensive phrases that include use cases and synonyms
    /// 
    /// Configuration requirements:
    /// - Both OllamaEmbedding and OllamaChat settings must be configured
    /// - Chat model should be capable of understanding and generating descriptive text
    /// - Embedding model should be optimized for semantic similarity (e.g., nomic-embed-text)
    /// </summary>
    public class EnhancedSemanticKernelToolIndexService : IToolIndexService
    {
        private readonly IMcpManager _mcpManager;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly SemanticKernelSettings _settings;
        private readonly ILogger<EnhancedSemanticKernelToolIndexService> _logger;
        private readonly InMemoryVectorStore _vectorStore;

        // Cache for fast tool lookup
        private readonly ConcurrentDictionary<string, IReadOnlyList<ToolInfo>> _toolCache = new();

        public EnhancedSemanticKernelToolIndexService(
            IMcpManager mcpManager,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IChatCompletionService chatCompletionService,
            SemanticKernelSettings settings,
            ILogger<EnhancedSemanticKernelToolIndexService> logger)
        {
            _mcpManager = mcpManager ?? throw new ArgumentNullException(nameof(mcpManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
            _chatCompletionService = chatCompletionService ?? throw new ArgumentNullException(nameof(chatCompletionService));
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
                var vectorResults = collection.SearchAsync(queryEmbedding, maxResults);

                var results = new List<ToolSearchResult>();

                foreach (var result in await vectorResults.ToListAsync())
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

                _logger.LogInformation("Enhanced semantic search returned {Count} results for query: {Query}",
                    sortedResults.Count, query);

                return sortedResults.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing enhanced semantic search for query: {Query}", query);
                throw;
            }
        }

        public async Task<string> CallExternalToolAsync(string serverName, string toolName, JsonElement parameters, CancellationToken cancellationToken = default)
        {
            var server = await _mcpManager.GetServerAsync(serverName, cancellationToken);
            if (server == null)
            {
                throw new InvalidOperationException($"Server '{serverName}' not found or not running. Is this the server your were given in the sample call provided be search results? If not, try again by following the instructions given.");
            }

            return await server.CallToolAsync(toolName, parameters, cancellationToken);
        }

        public async Task RefreshIndexAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing enhanced semantic tool index...");

                var collection = _vectorStore.GetCollection<string, ToolVectorRecord>("tool_index");
                await collection.EnsureCollectionExistsAsync();

                var runningServers = await _mcpManager.GetRunningServersAsync();
                var newCache = new ConcurrentDictionary<string, IReadOnlyList<ToolInfo>>();
                var toolInfoList = new List<(string serverName, string serverDescription, ToolInfo tool)>();

                // First, collect all tools and cache them
                foreach (var server in runningServers)
                {
                    var tools = server.AvailableToolsWithInfo;
                    newCache[server.Name] = tools;

                    foreach (var tool in tools)
                    {
                        toolInfoList.Add((server.Name, server.Description, tool));
                    }
                }

                _logger.LogInformation("Generating enhanced search phrases for {ToolCount} tools...", toolInfoList.Count);

                // Generate all phrases first using chat completions
                var phraseTasks = toolInfoList.Select(async toolInfo =>
                {
                    try
                    {
                        var phrase = await GenerateSearchPhraseAsync(
                            toolInfo.serverName,
                            toolInfo.serverDescription,
                            toolInfo.tool);
                        return (toolInfo, phrase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate phrase for tool {Server}.{Tool}, using fallback",
                            toolInfo.serverName, toolInfo.tool.Name);

                        // Fallback to simpler phrase generation
                        var fallbackPhrase = $"{toolInfo.tool.Name} tool for {toolInfo.tool.Description}. Available from {toolInfo.serverName} server.";
                        return (toolInfo, fallbackPhrase);
                    }
                });

                var toolsWithPhrases = await Task.WhenAll(phraseTasks);

                _logger.LogInformation("Generating embeddings for {ToolCount} enhanced phrases...", toolsWithPhrases.Length);

                // Now generate embeddings for all phrases
                var vectorRecords = new List<ToolVectorRecord>();

                // for debugging, log the phrases generated to a file alongside the tool info
                var debugLogPath = Path.Combine(AppContext.BaseDirectory, "EnhancedToolPhrases.log");
                if (File.Exists(debugLogPath))
                {
                    File.Delete(debugLogPath);
                }
                var logLines = new List<string>();

                foreach (var (toolInfo, phrase) in toolsWithPhrases)
                {
                    try
                    {
                        logLines.Add($"Server: {toolInfo.serverName}, Tool: {toolInfo.tool.Name}, Phrase: {phrase}");

                        var embedding = await _embeddingGenerator.GenerateAsync(phrase);
                        var record = CreateToolVectorRecord(
                            toolInfo.serverName,
                            toolInfo.tool,
                            phrase,
                            embedding);
                        vectorRecords.Add(record);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate embedding for tool {Server}.{Tool}",
                            toolInfo.serverName, toolInfo.tool.Name);
                    }
                }

                File.WriteAllLines(debugLogPath, logLines);

                // Upsert all vector records
                await collection.UpsertAsync(vectorRecords);

                // Update caches atomically
                _toolCache.Clear();
                foreach (var kvp in newCache)
                {
                    _toolCache[kvp.Key] = kvp.Value;
                }

                var totalTools = _toolCache.Values.Sum(tools => tools.Count);
                _logger.LogInformation("Enhanced semantic tool index refreshed: {ServerCount} servers, {ToolCount} total tools, {VectorRecords} vector records",
                    _toolCache.Count, totalTools, vectorRecords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing enhanced semantic tool index");
                throw;
            }
        }

        /// <summary>
        /// Generates an optimized search phrase for a tool using LLM chat completions.
        /// Uses the configurable prompt template from settings.
        /// </summary>
        private async Task<string> GenerateSearchPhraseAsync(string serverName, string serverDescription, ToolInfo tool)
        {
            var parametersText = tool.Parameters.Count > 0
                ? string.Join(", ", tool.Parameters.Select(p => $"{p.Name} ({p.Type}): {p.Description}"))
                : "No parameters";

            // Use the configurable prompt template from settings
            var prompt = string.Format(_settings.OllamaChat.PhraseGenerationPrompt,
                serverName,
                tool.Name,
                tool.Description);

            _logger.LogDebug("Generating search phrase for tool {Server}.{Tool}", serverName, tool.Name);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a tool documentation specialist that rewrites descriptions of tools.");
            chatHistory.AddUserMessage(prompt);

            // Create execution settings with low temperature for more deterministic phrase generation
            var executionSettings = new OllamaPromptExecutionSettings
            {
                Temperature = _settings.OllamaChat.Temperature // Use configured temperature for phrase generation
            };

            var response = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings);
            var rawResponse = response.Content == null ? tool.Description : response.Content.Trim();

            if (string.IsNullOrEmpty(rawResponse))
            {
                _logger.LogWarning("Empty response from chat completion for tool {Server}.{Tool}, using fallback",
                    serverName, tool.Name);
                return $"{tool.Name} tool for {tool.Description}. Available from {serverName} server.";
            }

            return rawResponse;
        }

        /// <summary>
        /// Creates a tool vector record with the generated phrase and embedding.
        /// </summary>
        private static ToolVectorRecord CreateToolVectorRecord(
            string serverName,
            ToolInfo tool,
            string searchPhrase,
            Embedding<float> embedding)
        {
            var parametersJson = JsonSerializer.Serialize(tool.Parameters);
            var parameterNames = string.Join(", ", tool.Parameters.Select(p => p.Name));

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