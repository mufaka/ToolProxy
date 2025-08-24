namespace ToolProxy.Chat.Models;

public class ChatConfiguration
{
    public OllamaConfiguration Ollama { get; set; } = new();
    public ToolProxyConfiguration ToolProxy { get; set; } = new();
    public AgentConfiguration Agent { get; set; } = new();
}

public class OllamaConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ModelName { get; set; } = "llama3.2:3b";
    public double Temperature { get; set; } = 0.7;
}

public class ToolProxyConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:3030";
    public string McpEndpoint { get; set; } = "/mcp";
}

public class AgentConfiguration
{
    public string SystemPrompt { get; set; } = string.Empty;
}

public record ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Content { get; init; } = string.Empty;
    public ChatRole Role { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum ChatRole
{
    User,
    Assistant,
    System
}