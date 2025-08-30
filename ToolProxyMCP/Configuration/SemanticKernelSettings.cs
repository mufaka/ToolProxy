namespace ToolProxy.Configuration
{
    public class SemanticKernelSettings
    {
        public VectorStoreSettings VectorStore { get; set; } = new();
        public OllamaEmbeddingSettings OllamaEmbedding { get; set; } = new();
        public OllamaChatSettings OllamaChat { get; set; } = new();
        public bool UseEnhancedPhraseGeneration { get; set; } = true;
    }

    public class VectorStoreSettings
    {
        public string CollectionName { get; set; } = "mcp_tools_vector_index";
        public int EmbeddingDimensions { get; set; } = 1536;
    }

    public class OllamaEmbeddingSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModelName { get; set; } = "mxbai-embed-large:latest";
    }

    public class OllamaChatSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string ModelName { get; set; } = "qwen2.5:7b-instruct";
        public float Temperature { get; set; } = 0.1f;
        public string PhraseGenerationPrompt { get; set; } = @"You are an expert at creating search phrases for semantic similarity matching. Create a comprehensive phrase that captures what the tool does and when someone would use it.

Tool Information:
Server: {0}
Tool Name: {1}
Description: {2}
Parameters: {3}

Requirements for the search phrase:
1. Create an imperative phrase for the tool name without using the exact tool name.
2. At most, only use nouns from the description once. Favor verbs and action-oriented language. If a noun, like 'memory', is used several times in the description, it can be used once in your response. We only want one imperative phrase covering that noun.
3. Be comprehensive but concise (2-3 sentences max)
4. Include, at the end, the server name and tool name for context. This should be the only place the tool name or server name is mentioned.
5. Avoid generic phrases that could apply to many tools. Be specific to this tool's functionality.

For example, if the tool is a 'WeatherTool' that 'fetches current weather data for a given location', a good phrase might be:
'Get current weather information for any city or region, including temperature, humidity, and conditions. Available from WeatherServer's WeatherTool.'

CRITICAL: Your response must contain ONLY the search phrases. Do not add explanations, introductions, or any other text. Start immediately with the phrase content.";
    }
}