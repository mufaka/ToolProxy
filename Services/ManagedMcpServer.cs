using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Text.Json;
using ToolProxy.Configuration;

namespace ToolProxy.Services
{
    public record ToolParameter(string Name, string Type, string Description, bool IsRequired);

    public record ToolInfo(string Name, string Description, IReadOnlyList<ToolParameter> Parameters);

    public class ManagedMcpServer : IManagedMcpServer, IDisposable
    {
        private readonly McpServerConfig _config;
        private readonly ILogger<ManagedMcpServer> _logger;
        private IMcpClient? _mcpClient;
        private IClientTransport? _clientTransport;
        private bool _disposed;
        private List<ToolInfo> _discoveredTools = new();
        private bool _toolsDiscovered = false;

        public ManagedMcpServer(McpServerConfig config, ILogger<ManagedMcpServer> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Name => _config.Name;
        public string Description => _config.Description;
        public bool IsEnabled => _config.Enabled;

        // Return discovered tool names if available, otherwise fall back to configuration
        public IReadOnlyList<string> AvailableTools =>
            _toolsDiscovered && _discoveredTools.Any()
                ? _discoveredTools.Select(t => t.Name).ToList().AsReadOnly()
                : _config.Tools.AsReadOnly();

        public IReadOnlyList<ToolInfo> AvailableToolsWithInfo =>
            _toolsDiscovered && _discoveredTools.Any()
                ? _discoveredTools.AsReadOnly()
                : _config.Tools.Select(t => new ToolInfo(t, "No description available", Array.Empty<ToolParameter>())).ToList().AsReadOnly();

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                _logger.LogWarning("MCP server {Name} is disabled", Name);
                return false;
            }

            if (_mcpClient != null)
            {
                _logger.LogInformation("MCP server {Name} is already running", Name);
                return true;
            }

            try
            {
                _logger.LogInformation("Starting MCP server {Name} with transport {Transport}", Name, _config.Transport);

                // Create appropriate client transport based on configuration
                _clientTransport = CreateClientTransport();

                // Set environment variables (for STDIO transport)
                if (_config.Transport.Equals("stdio", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var envVar in _config.Env)
                    {
                        Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
                    }
                }

                // Create MCP client using the official factory
                _mcpClient = await McpClientFactory.CreateAsync(_clientTransport);

                _logger.LogInformation("Started MCP server {Name} successfully", Name);

                // Discover available tools from the server
                await DiscoverToolsAsync(cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MCP server {Name}", Name);
                await CleanupAsync();
                return false;
            }
        }

        private IClientTransport CreateClientTransport()
        {
            return _config.Transport.ToLowerInvariant() switch
            {
                "stdio" => CreateStdioTransport(),
                "http" => CreateHttpTransport(),
                "streamable-http" => CreateHttpTransport(),
                "sse" => CreateHttpTransport(),
                _ => throw new InvalidOperationException($"Unsupported transport type: {_config.Transport}")
            };
        }

        private StdioClientTransport CreateStdioTransport()
        {
            if (string.IsNullOrEmpty(_config.Command))
            {
                throw new InvalidOperationException($"Command is required for STDIO transport in server {Name}");
            }

            _logger.LogDebug("Creating STDIO transport for {Name}: {Command} {Args}",
                Name, _config.Command, string.Join(" ", _config.Args));

            return new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = _config.Name,
                Command = _config.Command,
                Arguments = _config.Args,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            });
        }

        private SseClientTransport CreateHttpTransport()
        {
            if (string.IsNullOrEmpty(_config.Url))
            {
                throw new InvalidOperationException($"URL is required for HTTP transport in server {Name}");
            }

            if (!Uri.TryCreate(_config.Url, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"Invalid URL '{_config.Url}' for HTTP transport in server {Name}");
            }

            _logger.LogDebug("Creating HTTP transport for {Name}: {Url}", Name, _config.Url);

            return new SseClientTransport(new SseClientTransportOptions
            {
                Name = _config.Name,
                Endpoint = uri,
                TransportMode = HttpTransportMode.AutoDetect // Try Streamable HTTP first, fallback to SSE
            });
        }

        public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
        {
            if (_mcpClient != null)
            {
                await DiscoverToolsAsync(cancellationToken);
            }
        }

        private async Task DiscoverToolsAsync(CancellationToken cancellationToken)
        {
            if (_mcpClient == null)
            {
                _logger.LogWarning("Cannot discover tools for {Name}: MCP client not available", Name);
                return;
            }

            try
            {
                _logger.LogDebug("Discovering tools for MCP server {Name}", Name);

                // Use the official SDK to list tools
                var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

                var discoveredTools = new List<ToolInfo>();

                foreach (var tool in tools)
                {
                    var parameters = ParseInputSchema(tool);
                    // Use the Name property from AITool base class, not Metadata.Name
                    discoveredTools.Add(new ToolInfo(tool.Name, tool.Description, parameters));
                }

                if (discoveredTools.Any())
                {
                    _discoveredTools = discoveredTools;
                    _toolsDiscovered = true;
                    _logger.LogInformation("Discovered {Count} tools from MCP server {Name}: {Tools}",
                        discoveredTools.Count, Name, string.Join(", ", discoveredTools.Select(t => t.Name)));
                }
                else
                {
                    _logger.LogWarning("MCP server {Name} returned empty tools list, using configured tools", Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error discovering tools from MCP server {Name}, using configured tools", Name);
            }
        }

        private IReadOnlyList<ToolParameter> ParseInputSchema(McpClientTool tool)
        {
            var parameters = new List<ToolParameter>();

            try
            {
                var inputSchema = tool.JsonSchema;

                // Get required fields
                var requiredFields = new HashSet<string>();
                if (inputSchema.TryGetProperty("required", out var requiredArray) &&
                    requiredArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var requiredField in requiredArray.EnumerateArray())
                    {
                        var fieldName = requiredField.GetString();
                        if (!string.IsNullOrEmpty(fieldName))
                        {
                            requiredFields.Add(fieldName);
                        }
                    }
                }

                // Parse properties
                if (inputSchema.TryGetProperty("properties", out var properties) &&
                    properties.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in properties.EnumerateObject())
                    {
                        var paramName = property.Name;
                        var paramInfo = property.Value;

                        var paramType = "unknown";
                        if (paramInfo.TryGetProperty("type", out var typeElement))
                        {
                            // Handle both string and array of strings for type
                            if (typeElement.ValueKind == JsonValueKind.String)
                            {
                                paramType = typeElement.GetString() ?? "unknown";
                            }
                            else if (typeElement.ValueKind == JsonValueKind.Array)
                            {
                                // If type is an array, use the first element
                                var firstType = typeElement.EnumerateArray().FirstOrDefault();
                                if (firstType.ValueKind == JsonValueKind.String)
                                {
                                    paramType = firstType.GetString() ?? "unknown";
                                }
                            }
                        }

                        var paramDescription = "No description available";
                        if (paramInfo.TryGetProperty("description", out var descElement))
                        {
                            paramDescription = descElement.GetString() ?? paramDescription;
                        }

                        var isRequired = requiredFields.Contains(paramName);

                        parameters.Add(new ToolParameter(paramName, paramType, paramDescription, isRequired));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error parsing input schema for tool {ToolName}, continuing with empty parameters. {JsonSchema}", tool.Name, tool.JsonSchema);
            }

            return parameters.AsReadOnly();
        }

        public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
        {
            await CleanupAsync();
            _logger.LogInformation("Stopped MCP server {Name}", Name);
            return true;
        }

        private async Task CleanupAsync()
        {
            // Reset discovery state
            _toolsDiscovered = false;
            _discoveredTools.Clear();

            if (_mcpClient != null)
            {
                await _mcpClient.DisposeAsync();
                _mcpClient = null;
            }

            // Transport is managed by the MCP client, no need to dispose manually
            _clientTransport = null;
        }

        public async Task<string> CallToolAsync(string toolName, JsonElement parameters, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException($"MCP server {Name} is disabled");
            }

            if (_mcpClient == null)
            {
                throw new InvalidOperationException($"MCP server {Name} is not running");
            }

            if (!AvailableTools.Contains(toolName))
            {
                throw new ArgumentException($"Tool {toolName} is not available on server {Name}. Available tools: {string.Join(", ", AvailableTools)}");
            }

            try
            {
                _logger.LogDebug("Calling tool {ToolName} on MCP server {Name}", toolName, Name);

                // Convert JsonElement parameters to Dictionary<string, object?>
                var argumentsDict = new Dictionary<string, object?>();
                if (parameters.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in parameters.EnumerateObject())
                    {
                        argumentsDict[property.Name] = JsonElementToObject(property.Value);
                    }
                }

                // Get the specific tool from the client
                var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
                var tool = tools.FirstOrDefault(t => t.Name == toolName);

                if (tool == null)
                {
                    throw new InvalidOperationException($"Tool {toolName} not found");
                }

                // Use the official SDK to call the tool
                var result = await tool.CallAsync(argumentsDict, cancellationToken: cancellationToken);

                // Extract text content from the result
                var textContent = result.Content
                    .Where(c => c.Type == "text")
                    .Select(c => c is ModelContextProtocol.Protocol.TextContentBlock textBlock ? textBlock.Text : "")
                    .Where(text => !string.IsNullOrEmpty(text));

                return string.Join("\n", textContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling tool {ToolName} on server {Name}", toolName, Name);
                throw new InvalidOperationException($"Error calling tool {toolName}: {ex.Message}", ex);
            }
        }

        private static object? JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.ToString()
            };
        }

        public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_mcpClient != null);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CleanupAsync().GetAwaiter().GetResult();
                _disposed = true;
            }
        }
    }
}
