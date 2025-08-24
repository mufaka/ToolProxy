using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ModelContextProtocol.Client;
using System.Runtime.CompilerServices;
using System.Text;
using ToolProxy.Chat.Models;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates

namespace ToolProxy.Chat.Services;

public interface IKernelAgentService
{
    Task InitializeAsync();
    Task<string> InvokeAsync(string prompt);
    IAsyncEnumerable<string> InvokeStreamingAsync(string prompt);
    Task<List<ChatMessage>> GetHistoryAsync();
    Task ClearHistoryAsync();
}

public class KernelAgentService : IKernelAgentService
{
    private readonly ChatConfiguration _config;
    private readonly ILogger<KernelAgentService> _logger;
    private Kernel? _kernel;
    private IMcpClient? _mcpClient;
    private readonly List<ChatMessage> _history = new();
    private readonly StringBuilder _conversationHistory = new();

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

            // Add MCP tools as kernel functions
            if (availableTools.Any())
            {
                var kernelFunctions = availableTools.Select(tool => tool.AsKernelFunction());
                builder.Plugins.AddFromFunctions("ToolProxy", kernelFunctions);
                _logger.LogInformation("Added {FunctionCount} functions to kernel", availableTools.Count);
            }

            _kernel = builder.Build();

            // Initialize conversation with system prompt
            _conversationHistory.AppendLine(_config.Agent.SystemPrompt);
            _conversationHistory.AppendLine();

            _logger.LogInformation("Kernel Agent Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Kernel Agent Service");
            throw;
        }
    }

    public async Task<string> InvokeAsync(string prompt)
    {
        if (_kernel == null)
            throw new InvalidOperationException("Kernel not initialized. Call InitializeAsync first.");

        // Add user message to history
        var userMessage = new ChatMessage
        {
            Content = prompt,
            Role = ChatRole.User,
            Timestamp = DateTime.UtcNow
        };
        _history.Add(userMessage);

        // Build conversation prompt
        _conversationHistory.AppendLine($"User: {prompt}");

        var fullPrompt = _conversationHistory.ToString();

        // Create execution settings - fix Temperature and remove MaxTokens
        var executionSettings = new OllamaPromptExecutionSettings
        {
            Temperature = (float)_config.Ollama.Temperature
        };

        // Invoke kernel
        var result = await _kernel.InvokePromptAsync(fullPrompt, new(executionSettings)).ConfigureAwait(false);

        var assistantResponse = result.ToString();

        // Add assistant response to history
        var assistantMessage = new ChatMessage
        {
            Content = assistantResponse,
            Role = ChatRole.Assistant,
            Timestamp = DateTime.UtcNow
        };
        _history.Add(assistantMessage);

        // Update conversation history
        _conversationHistory.AppendLine($"Assistant: {assistantResponse}");
        _conversationHistory.AppendLine();

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
            Timestamp = DateTime.UtcNow
        };
        _history.Add(userMessage);

        // Build conversation prompt
        _conversationHistory.AppendLine($"User: {prompt}");

        var fullPrompt = _conversationHistory.ToString();

        // Create execution settings - fix Temperature and remove MaxTokens
        var executionSettings = new OllamaPromptExecutionSettings
        {
            Temperature = (float)_config.Ollama.Temperature
        };

        var responseBuilder = new StringBuilder();

        // Stream the response
        await foreach (var chunk in _kernel.InvokePromptStreamingAsync(fullPrompt, new(executionSettings)))
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
            Timestamp = DateTime.UtcNow
        };
        _history.Add(assistantMessage);

        // Update conversation history
        _conversationHistory.AppendLine($"Assistant: {fullResponse}");
        _conversationHistory.AppendLine();
    }

    public Task<List<ChatMessage>> GetHistoryAsync() =>
        Task.FromResult(_history.ToList());

    public Task ClearHistoryAsync()
    {
        _history.Clear();
        _conversationHistory.Clear();
        _conversationHistory.AppendLine(_config.Agent.SystemPrompt);
        _conversationHistory.AppendLine();
        return Task.CompletedTask;
    }
}