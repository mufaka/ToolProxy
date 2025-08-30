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
                throw new InvalidOperationException($"Server '{serverName}' not found or not running. Is this the server your were given in the sample call provided be search results? If not, try again by following the instructions given.");
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

        /*
            How we embed a tool for semantic search is extremely important. Embeddings are stored in a manner that will be "close" to queries that are semantically 
            similar. We want to provide as much relevant context as possible without overwhelming the model with too much information. There is a balance to be struck 
            between providing enough detail about the tool and its parameters, while keeping the input concise.

            We also have to contend with the fact that multiple tools from different servers may have descriptions that are semantically similar but contextually different.

            eg: 
                A tool named addMemory,  described as "Store a memory for a project (uses context project if not provided)" 
                A tool named write_memory, described as "Write some information about this project that can be useful for future tasks to a memory in md format.\nThe memory name should be meaningful."

            Both tools are about storing information, but the context (project memory vs general memory) is different. A semantic search for "save a memory" would return both
            tools as relevant, but the ambiguity of the search could lead to confusion about which tool to use.

            Using and embedding input of: var embeddingInput = $"{serverName} has a tool named {tool.Name} that can be used for: {tool.Description}."; Is aimed at helping 
            distinguish between tools by providing context, but that also introduces the potential of picking tools from the server that might not be relevant to the task 
            by ranking them higher due to the server context.

            eg: "save a memory" returns the following results:

              "tools": [
                {
                  "serverName": "Project Pilot",
                  "name": "addMemory",
                  "description": "Store a memory for a project (uses context project if not provided)",
                  "relevanceScore": 0.62376606,
                    ...
                },
                {
                  "serverName": "Serena",
                  "name": "write_memory",
                  "description": "Write some information about this project that can be useful for future tasks to a memory in md format.\nThe memory name should be meaningful.",
                  "relevanceScore": 0.58559597,
                    ...
                }
              ]

            eg: "serena, save a memory" returns the following results:

              "tools": [
                {
                  "serverName": "Serena",
                  "name": "list_memories",
                  "description": "List available memories. Any memory can be read using the `read_memory` tool.",
                  "relevanceScore": 0.64463073,
                  "parameters": []
                },
                {
                  "serverName": "Serena",
                  "name": "prepare_for_new_conversation",
                  "description": "Instructions for preparing for a new conversation. This tool should only be called on explicit user request.",
                  "relevanceScore": 0.6247413,
                  "parameters": []
                },
                {
                  "serverName": "Serena",
                  "name": "think_about_collected_information",
                  "description": "Think about the collected information and whether it is sufficient and relevant.\nThis tool should ALWAYS be called after you have completed a non-trivial sequence of searching steps like\nfind_symbol, find_referencing_symbols, search_files_for_pattern, read_file, etc.",
                  "relevanceScore": 0.6163351,
                  "parameters": []
                },
                {
                  "serverName": "Serena",
                  "name": "read_memory",
                  "description": "Read the content of a memory file. This tool should only be used if the information\nis relevant to the current task. You can infer whether the information\nis relevant from the memory file name.\nYou should not read the same memory file multiple times in the same conversation.",
                  "relevanceScore": 0.6146194,
                    ...
                },
                {
                  "serverName": "Serena",
                  "name": "write_memory",
                  "description": "Write some information about this project that can be useful for future tasks to a memory in md format.\nThe memory name should be meaningful.",
                  "relevanceScore": 0.6106719,
                    ...
                }
              ]

            This is problematic because it's clear that even though the second result set is correctly picking the right server, it is ranking the tools in a way that is not helpful.
            This issue here is that the write_memory tools description isn't sufficiently distinguishing itself as a save action.

            Playing around with the embedding input does seem to help. eg: 
                var embeddingInput = $"\"{tool.Description}\" can be performed by the tool named \"{tool.Name}\". It is available from the server: {serverName}.";

            But ultimately, this is going to be a limitation on the MCP server tool names and descriptions. If they aren't sufficiently descriptive and distinct, the embedding model will struggle 
            to differentiate them.

            One thought is to have an LLM use the server name, tool name, and description to generate an embedding phrase geared for being semantically searched against. BUt
            that then introduces an external dependency. Maybe we make that a configuration option. If this is done, we need to generate all the phrases first and then do
            the embedding because we don't want Ollama switching models back and forth for every tool.
        */
        private async Task<ToolVectorRecord> CreateToolVectorRecordAsync(string serverName, string serverDescription, ToolInfo tool)
        {
            var parametersJson = JsonSerializer.Serialize(tool.Parameters);
            var parameterNames = string.Join(", ", tool.Parameters.Select(p => p.Name));
            var parameterDescriptions = string.Join("; ", tool.Parameters.Select(p => $"{p.Name}: {p.Description}"));

            var embeddingInput = $"\"{tool.Name}\" that is used for \"{tool.Description}\". \"{tool.Name}\" is available from the server: {serverName}.";


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