using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text;
using System.Text.Json;
using ToolProxy.Chat.Models;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace ToolProxy.Chat.Services;

public record ToolInfo(string Name, string Description, Dictionary<string, object> Parameters);
public record ServerInfo(string Name, string Description, List<ToolInfo> Tools);

public interface IKernelAgentService
{
    Task InitializeAsync();
    Task<string> InvokeAsync(string prompt);
    IAsyncEnumerable<string> InvokeStreamingAsync(string prompt);
    // Task<List<ChatMessage>> GetHistoryAsync(); // see TODO below in implementation
    Task ClearHistoryAsync();
    Task<List<ServerInfo>> GetAvailableToolsAsync();
    Task RefreshToolsAsync();
}

public class KernelAgentService : IKernelAgentService
{
    private readonly ChatConfiguration _config;
    private readonly ILogger<KernelAgentService> _logger;
    private Kernel? _kernel;
    private IMcpClient? _mcpClient;
    private ChatCompletionAgent? _agent;
    private ChatHistoryAgentThread? _agentThread;
    private List<ServerInfo> _cachedTools = new();

    public KernelAgentService(ChatConfiguration config, ILogger<KernelAgentService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Kernel Agent Service...");

            // Create MCP client to connect to ToolProxy server
            var mcpEndpoint = new Uri($"{_config.ToolProxy.BaseUrl}{_config.ToolProxy.McpEndpoint}");
            var transport = new SseClientTransport(new SseClientTransportOptions
            {
                Name = "ToolProxy",
                Endpoint = mcpEndpoint
            });

            _mcpClient = await McpClientFactory.CreateAsync(transport);
            _logger.LogInformation("Connected to ToolProxy MCP server at {Endpoint}", mcpEndpoint);

            // Get available tools from ToolProxy
            var availableTools = await _mcpClient.ListToolsAsync();
            _logger.LogInformation("Found {ToolCount} available tools from ToolProxy", availableTools.Count);

            // Cache the tools for UI display
            await CacheToolsAsync();

            // Create kernel with Ollama and tools
            var builder = Kernel.CreateBuilder();
            builder.AddOllamaChatCompletion(
                modelId: _config.Ollama.ModelName,
                endpoint: new Uri(_config.Ollama.BaseUrl));


            _kernel = builder.Build();

            // Add MCP tools as kernel functions
            if (availableTools.Any())
            {
                var kernelFunctions = availableTools.Select(tool => tool.AsKernelFunction());
                _kernel.Plugins.AddFromFunctions("ToolProxy", kernelFunctions);
                _logger.LogInformation("Added {FunctionCount} functions to kernel", availableTools.Count);
            }

            InitializeAgent();

            _logger.LogInformation("Kernel Agent Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Kernel Agent Service");
            throw;
        }
    }

    private async Task CacheToolsAsync()
    {
        try
        {
            if (_mcpClient == null) return;

            // Call the new JSON-based tool to get all servers and tools
            var allToolsResult = await _mcpClient.CallToolAsync("list_all_servers_and_tools_json", new Dictionary<string, object>());

            if (allToolsResult.Content.Count > 0)
            {
                if (allToolsResult.Content[0] is TextContentBlock textBlock)
                {
                    _cachedTools = ParseToolsFromJsonResult(textBlock.Text);
                    _logger.LogInformation("Cached {ServerCount} servers with tools for UI display", _cachedTools.Count);
                }
                else
                {
                    _logger.LogWarning("No content returned for server list.");
                }
            }
            else
            {
                _logger.LogWarning("No content returned for server list.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache tools for UI display, falling back to semantic search");
        }
    }

    private List<ServerInfo> ParseToolsFromJsonResult(string jsonResult)
    {
        try
        {
            var servers = new List<ServerInfo>();

            using var document = JsonDocument.Parse(jsonResult);
            var root = document.RootElement;

            // Check if the response contains an error
            if (root.TryGetProperty("error", out _))
            {
                _logger.LogWarning("JSON tool response contains error, using fallback parsing");
                return new List<ServerInfo>();
            }

            // Parse the servers array
            if (root.TryGetProperty("servers", out var serversElement) &&
                serversElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var serverElement in serversElement.EnumerateArray())
                {
                    if (!serverElement.TryGetProperty("serverName", out var serverNameElement) ||
                        !serverElement.TryGetProperty("tools", out var toolsElement))
                        continue;

                    var serverName = serverNameElement.GetString() ?? "Unknown Server";
                    var tools = new List<ToolInfo>();

                    if (toolsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var toolElement in toolsElement.EnumerateArray())
                        {
                            var toolName = toolElement.TryGetProperty("name", out var nameElement)
                                ? nameElement.GetString() ?? "Unknown Tool"
                                : "Unknown Tool";

                            var toolDescription = toolElement.TryGetProperty("description", out var descElement)
                                ? descElement.GetString() ?? "No description available"
                                : "No description available";

                            // Parse parameters for additional metadata (optional)
                            var parameters = new Dictionary<string, object>();
                            if (toolElement.TryGetProperty("parameters", out var paramsElement) &&
                                paramsElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var paramElement in paramsElement.EnumerateArray())
                                {
                                    if (paramElement.TryGetProperty("name", out var paramNameElement))
                                    {
                                        var paramName = paramNameElement.GetString() ?? "";
                                        var paramType = paramElement.TryGetProperty("type", out var typeElement)
                                            ? typeElement.GetString() ?? "unknown"
                                            : "unknown";
                                        var required = paramElement.TryGetProperty("required", out var reqElement)
                                            && reqElement.GetBoolean();

                                        parameters[paramName] = new { type = paramType, required = required };
                                    }
                                }
                            }

                            tools.Add(new ToolInfo(toolName, toolDescription, parameters));
                        }
                    }

                    var serverDescription = $"{serverName} Server ({tools.Count} tools)";
                    servers.Add(new ServerInfo(serverName, serverDescription, tools));
                }
            }

            _logger.LogInformation("Successfully parsed {ServerCount} servers from JSON response", servers.Count);
            return servers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JSON tools result: {JsonResult}", jsonResult);
            return new List<ServerInfo>();
        }
    }

    private List<ServerInfo> ParseToolsFromResult(string result)
    {
        var servers = new Dictionary<string, List<ToolInfo>>();

        // Parse the semantic search result to extract server.tool information
        var lines = result.Split('\n');

        foreach (var line in lines)
        {
            // Look for lines that match the pattern "ServerName.ToolName (Score: X.XXX)"
            if (line.Contains('.') && line.Contains("(Score:"))
            {
                var parts = line.Split('.');
                if (parts.Length >= 2)
                {
                    var serverName = parts[0].Trim();
                    var toolPart = parts[1];
                    var toolName = toolPart.Split(' ').FirstOrDefault()?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(serverName) && !string.IsNullOrEmpty(toolName))
                    {
                        if (!servers.ContainsKey(serverName))
                        {
                            servers[serverName] = new List<ToolInfo>();
                        }

                        // Find the description on the next line
                        var description = "No description available";
                        var currentIndex = Array.IndexOf(lines, line);
                        if (currentIndex + 1 < lines.Length)
                        {
                            var nextLine = lines[currentIndex + 1].Trim();
                            if (nextLine.StartsWith("    ") && !nextLine.StartsWith("    Parameters:"))
                            {
                                description = nextLine.Trim();
                            }
                        }

                        servers[serverName].Add(new ToolInfo(toolName, description, new Dictionary<string, object>()));
                    }
                }
            }
        }

        return servers.Select(kvp => new ServerInfo(kvp.Key, $"{kvp.Key} Server", kvp.Value)).ToList();
    }

    public async Task<List<ServerInfo>> GetAvailableToolsAsync()
    {
        if (!_cachedTools.Any())
        {
            await CacheToolsAsync();
        }
        return _cachedTools;
    }

    public async Task RefreshToolsAsync()
    {
        try
        {
            if (_mcpClient != null)
            {
                // Call the refresh tool index first
                await _mcpClient.CallToolAsync("refresh_tool_index", new Dictionary<string, object>());

                // Re-cache the tools using the new JSON method
                await CacheToolsAsync();

                _logger.LogInformation("Successfully refreshed tool index");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh tools");
            throw;
        }
    }

    private void InitializeAgent()
    {
        // Create execution settings
        var executionSettings = new OllamaPromptExecutionSettings
        {
            Temperature = (float)_config.Ollama.Temperature,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        // Create the ChatCompletionAgent with system prompt and kernel
        _agent = new()
        {
            Instructions = _config.Agent.SystemPrompt,
            Name = "ToolProxyAgent",
            Kernel = _kernel!, // kernel cannot be null here
            Arguments = new KernelArguments(executionSettings),
        };

        _agentThread = new ChatHistoryAgentThread();
    }

    public async Task<string> InvokeAsync(string prompt)
    {
        if (_kernel == null)
            throw new InvalidOperationException("Kernel not initialized. Call InitializeAsync first.");

        // can we be smarter about giving hints on the tools to use based on the prompt?
        // if a prompt names a specific server, we should coerce the LLM to include that name with the semantic search call.
        // but do we need to know the intent of the server mention? what if the server name is generic, like 'fetch'?

        // maybe we can just tell the LLM to be overly verbose when using semantic search?

        // Add user message to history
        var userMessage = new ChatMessage
        {
            Content = prompt,
            Role = ChatRole.User, // what is the difference between ChatRole and AuthorRole?
            Timestamp = DateTime.Now
        };

        // Get the response from the agent (it returns IAsyncEnumerable)
        var responseBuilder = new StringBuilder();
        await foreach (var responseItem in _agent!.InvokeAsync(prompt, _agentThread))
        {
            // Try to access the actual content from the response item
            if (responseItem.Message.Content != null && !String.IsNullOrWhiteSpace(responseItem.Message.Content))
            {
                responseBuilder.Append(responseItem.Message.Content);
            }
        }

        // Extract the complete response content
        var assistantResponse = responseBuilder.ToString();

        // Add assistant response to history
        var assistantMessage = new ChatMessage
        {
            Content = assistantResponse,
            Role = ChatRole.Assistant,
            Timestamp = DateTime.Now
        };

        return assistantResponse;
    }

    public async IAsyncEnumerable<string> InvokeStreamingAsync(string prompt)
    {
        if (_kernel == null)
            throw new InvalidOperationException("Kernel not initialized. Call InitializeAsync first.");

        // Add user message to history
        var userMessage = new ChatMessage
        {
            Content = prompt,
            Role = ChatRole.User,
            Timestamp = DateTime.Now
        };

        // Create execution settings - fix Temperature and remove MaxTokens
        var executionSettings = new OllamaPromptExecutionSettings
        {
            Temperature = (float)_config.Ollama.Temperature
        };

        var responseBuilder = new StringBuilder();

        // Stream the response
        await foreach (var chunk in _kernel.InvokePromptStreamingAsync(prompt, new(executionSettings)))
        {
            var content = chunk.ToString();
            responseBuilder.Append(content);
            yield return content;
        }

        var fullResponse = responseBuilder.ToString();

        // Add assistant response to history
        var assistantMessage = new ChatMessage
        {
            Content = fullResponse,
            Role = ChatRole.Assistant,
            Timestamp = DateTime.Now
        };
    }

    // TODO: Bring this back but use _agentThread to get history
    /*
    public Task<List<ChatMessage>> GetHistoryAsync() =>
        Task.FromResult(_history.ToList());
    */

    public Task ClearHistoryAsync()
    {
        InitializeAgent();
        return Task.CompletedTask;
    }
}