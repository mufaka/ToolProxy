namespace ToolProxy.Configuration
{
    public class SemanticKernelSettings
    {
        public VectorStoreSettings VectorStore { get; set; } = new();
        public OllamaSettings Ollama { get; set; } = new();
    }

    public class VectorStoreSettings
    {
        public string CollectionName { get; set; } = "mcp_tools_vector_index";
        public int EmbeddingDimensions { get; set; } = 1536;
    }

    public class OllamaSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModelName { get; set; } = "mxbai-embed-large:latest";
    }
}