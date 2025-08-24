namespace ToolProxy.Configuration
{
    public class AppSettings
    {
        public List<McpServerConfig> McpServers { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public SemanticKernelSettings SemanticKernel { get; set; } = new();
    }
}
