using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ModelContextProtocol.Client;
using System.Text;
using ToolProxy.Chat.Models;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace ToolProxy.Chat.Services;

public interface IKernelAgentService
{
    Task InitializeAsync();
    Task<string> InvokeAsync(string prompt);
    IAsyncEnumerable<string> InvokeStreamingAsync(string prompt);
    // Task<List<ChatMessage>> GetHistoryAsync(); // see TODO below in implementation
    Task ClearHistoryAsync();
}

public class KernelAgentService : IKernelAgentService
{
    private readonly ChatConfiguration _config;
    private readonly ILogger<KernelAgentService> _logger;
    private Kernel? _kernel;
    private IMcpClient? _mcpClient;
    private ChatCompletionAgent? _agent;
    private ChatHistoryAgentThread? _agentThread;

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
            if (responseItem.Message.Content != null)
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