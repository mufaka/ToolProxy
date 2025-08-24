namespace ToolProxy.Configuration
{
    public class McpServerConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Transport configuration
        public string Transport { get; set; } = "stdio"; // "stdio" or "http"
        public string? Url { get; set; } // Required for HTTP transport

        // STDIO transport configuration
        public string Command { get; set; } = string.Empty;
        public List<string> Args { get; set; } = new();
        public Dictionary<string, string> Env { get; set; } = new();

        public bool Enabled { get; set; } = true;
        public List<string> Tools { get; set; } = new();
    }
}
