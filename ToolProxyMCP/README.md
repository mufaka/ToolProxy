# ToolProxy - MCP Server with Semantic Kernel Integration

A sophisticated Model Context Protocol (MCP) server that acts as a proxy and aggregator for multiple external MCP servers, enhanced with AI-powered semantic search capabilities using Microsoft Semantic Kernel and Ollama embeddings.

## Features

- **Multi-Server MCP Proxy**: Connect to and manage multiple external MCP servers simultaneously
- **Enhanced Semantic Tool Search**: AI-powered tool discovery using vector embeddings, natural language queries, and LLM-generated search phrases
- **Multiple Transport Support**: STDIO, HTTP, and SSE transport protocols
- **Tool Discovery**: Automatic discovery and indexing of tools from connected MCP servers
- **REST API**: HTTP endpoints for health checks and tool information
- **Real-time Tool Refresh**: Dynamic updating of tool indexes without server restart
- **Vector-Based Search**: Semantic similarity search powered by Ollama embeddings
- **Intelligent Phrase Generation**: Uses chat completions to generate optimized search phrases for improved semantic matching

## Motivation
The Model Context Protocol (MCP) enables AI systems to interact with external tools and services. However, as the number of available MCP servers grows, discovering the right tool for a specific task becomes challenging. This project addresses this by providing a unified MCP server that aggregates multiple external servers and enhances tool discovery through semantic search.

In order for LLMs to utilize tools, they need to know which tools are available and relevant to their tasks. This is usually done by including information about all of the available tools in the context for the requests. Your context size grows with the number of tools and can lead to less precision when selecting a tool or agent "forgetfulness" in multi-step workflows.

The ToolProxy MCP server aims to solve this problem by limiting the number of tools present on a per request basis. With proper prompting, the agent and LLM can be instructed search for tools to use based on the task at hand. This allows for a much larger number of tools to be available without overwhelming the LLM with too much context.

## Prompt Example
Something like the following should exist in your agent instructions or prompt template. Customize as needed for the tools you want to highlight.
```
The tool-proxy tool has a variety of tools available to you. You will need to use it's semantic search feature to find tools that are relevant to what you are doing. 
You should always ask for external tools from tool-proxy when you need to manage projects, perform sequential thinking for help in planning, find documentation, 
and various other types of activities. Follow any prompts those tools give you.
```

In longer sessions, you may want/need to remind the agent of this capability periodically.
```
Remember that you can use the tool-proxy tool to search for tools that can help you with your tasks.
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     ToolProxy MCP Server                    │
├─────────────────────────────────────────────────────────────┤
│  Enhanced Local Tools                                       │
│  ├─ search_tools_semantic (AI-powered search)               │
│  ├─ list_all_external_tools                                 │
│  ├─ call_external_tool                                      │
│  └─ refresh_tool_index                                      │
├─────────────────────────────────────────────────────────────┤
│  Semantic Kernel Tool Index Service                         │
│  ├─ Vector Store (In-Memory)                                │
│  ├─ Ollama Text Embeddings                                  │
│  └─ Cosine Similarity Search                                │
├─────────────────────────────────────────────────────────────┤
│  MCP Manager                                                │
│  ├─ Server Lifecycle Management                             │
│  ├─ Tool Discovery & Caching                                │
│  └─ Multi-Transport Support                                 │
├─────────────────────────────────────────────────────────────┤
│  External MCP Servers                                       │
│  ├─ Context7 (Documentation)                                │
│  ├─ Sequential Thinking                                     │
│  ├─ Serena (Language Server Protocol)                       │
│  └─ Custom MCP Servers...                                   │
└─────────────────────────────────────────────────────────────┘
```

## Available Tools

### Built-in Tools

1. **`search_tools_semantic`** - AI-powered semantic search across all connected tools
   - Uses natural language queries to find relevant tools
   - Powered by vector embeddings and similarity matching
   - Returns ranked results with relevance scores

2. **`list_all_external_tools`** - List all available tools from connected MCP servers
   - Provides comprehensive tool inventory
   - Shows server associations and tool details
   - Indicates semantic search availability

3. **`call_external_tool`** - Execute tools from external MCP servers
   - Proxy tool execution with parameter forwarding
   - Supports all tool types from connected servers
   - Handles JSON parameter marshaling

4. **`refresh_tool_index`** - Refresh the tool index without server restart
   - Updates vector embeddings for new tools
   - Rebuilds semantic search index
   - Returns updated tool counts

### External MCP Servers (Configurable)

- **Context7**: Up-to-date library documentation and context
- **Sequential Thinking**: Complex task decomposition and reasoning
- **Serena**: Language Server Protocol integration for code analysis
- **Custom Servers**: Add your own MCP servers via configuration

## Configuration

### appsettings.json

```json
{
  "McpServer": {
    "Host": "localhost",
    "Port": 3030,
    "IdleTimeoutMinutes": 120,
    "MaxIdleSessions": 100000,
    "Stateless": false
  },
  "SemanticKernel": {
    "VectorStore": {
      "CollectionName": "mcp_tools_vector_index",
      "EmbeddingDimensions": 1536
    },
    "OllamaEmbedding": {
      "BaseUrl": "http://localhost:11434",
      "ModelName": "nomic-embed-text"
    },
    "OllamaChat": {
      "BaseUrl": "http://localhost:11434",
      "ModelName": "qwen2.5:7b-instruct",
      "Temperature": 0.1,
      "PhraseGenerationPrompt": "Create a search phrase for: {1} from {0}. Description: {2}. Parameters: {3}. Return only the phrase."
    },
    "UseEnhancedPhraseGeneration": true
  },
  "McpServers": [
    {
      "Name": "context7",
      "Description": "Context7 Up To Date library documentation",
      "Transport": "stdio",
      "Command": "cmd.exe",
      "Args": [ "/c", "npx", "-y", "@upstash/context7-mcp@latest" ],
      "Enabled": true,
      "Tools": []
    }
  ],
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Information"
    }
  }
}
```

### Enhanced Phrase Generation

The enhanced phrase generation feature improves semantic search accuracy by using an LLM to generate optimized search phrases for each tool before creating embeddings. This approach provides:

#### Benefits
- **Better Semantic Matching**: LLM-generated phrases capture tool purposes and use cases more comprehensively
- **Improved Search Results**: Higher relevance scores for semantically related tools
- **Enhanced Discoverability**: Tools are found even when queries use different terminology
- **Context-Aware Phrases**: Generated phrases include synonyms and alternative terms

#### Configuration Options
- **`UseEnhancedPhraseGeneration`**: Enable/disable the enhanced service (default: `true`)
- **`OllamaChat.ModelName`**: Chat model for phrase generation (e.g., `qwen2.5:7b-instruct`)
- **`OllamaChat.Temperature`**: Temperature for phrase generation (default: `0.1` for deterministic results)
- **`OllamaChat.PhraseGenerationPrompt`**: Customizable prompt template for phrase generation (supports placeholders: `{0}` = Server, `{1}` = Tool Name, `{2}` = Description, `{3}` = Parameters)
- **`OllamaEmbedding.ModelName`**: Embedding model for vector search (e.g., `nomic-embed-text`)

#### Performance Considerations
- **Initial Index Build**: Takes longer as phrases are generated for each tool
- **Memory Usage**: Slightly higher due to additional chat completion service
- **Model Switching**: Optimized to generate all phrases before embeddings to minimize Ollama model switching overhead

#### Fallback Mechanism
If phrase generation fails for any tool, the service automatically falls back to a simpler phrase construction to ensure robust operation.

#### Customizing the Phrase Generation Prompt
The prompt used for generating search phrases can be customized via the `PhraseGenerationPrompt` setting. The prompt template supports the following placeholders:
- **`{0}`**: Server name
- **`{1}`**: Tool name  
- **`{2}`**: Tool description
- **`{3}`**: Tool parameters (formatted as "name (type): description")

When customizing the prompt, ensure it:
1. Instructs the LLM to return only the phrase (no explanations)
2. Provides clear requirements for the phrase structure
3. Uses `string.Format` compatible placeholders
4. Maintains the critical instruction to avoid adding explanatory text

Example custom prompt:
```json
"PhraseGenerationPrompt": "Create a search phrase for this tool.\n\nTool: {1} from {0}\nDescription: {2}\nParameters: {3}\n\nReturn only the phrase, nothing else."
```

### MCP Server Configuration Options

#### STDIO Transport
```json
{
  "Name": "my-server",
  "Description": "Description of the server",
  "Transport": "stdio",
  "Command": "path/to/executable",
  "Args": ["arg1", "arg2"],
  "Env": {
    "ENV_VAR": "value"
  },
  "Enabled": true,
  "Tools": []
}
```

#### HTTP Transport
```json
{
  "Name": "http-server",
  "Description": "HTTP-based MCP server",
  "Transport": "http",
  "Url": "http://localhost:8080/mcp",
  "Enabled": true,
  "Tools": []
}
```

## Getting Started

### Prerequisites

1. **.NET 9.0 SDK**
2. **Ollama** (for semantic search)
   - Install Ollama: https://ollama.ai/download
   - Pull the embedding model: `ollama pull nomic-embed-text`
3. **Node.js** (for npm-based MCP servers)
4. **Python with uvx** (for Python-based MCP servers)

### Installation

1. **Clone and build the project:**
   ```bash
   git clone <repository-url>
   cd ToolProxy
   dotnet build
   ```

2. **Configure your MCP servers** in `appsettings.json`

3. **Start Ollama** (if using semantic search):
   ```bash
   ollama serve
   ollama pull nomic-embed-text  # For embeddings
   ollama pull qwen2.5:7b-instruct  # For enhanced phrase generation (optional)
   ```

4. **Run the server:**
   ```bash
   dotnet run
   ```

### Verification

1. **Health Check**: `http://localhost:3030/health`
2. **Tool Index Info**: `http://localhost:3030/tool-index-info`
3. **MCP Endpoint**: `http://localhost:3030/mcp`

## Usage Examples

### Connecting as MCP Client

Use any MCP-compatible client to connect to `http://localhost:3030/mcp`.

### Semantic Tool Search

```json
{
  "tool": "search_tools_semantic",
  "parameters": {
    "query": "find tools that can help me analyze code or understand functions",
    "maxResults": 5,
    "minRelevanceScore": 0.3
  }
}
```

### List All Tools

```json
{
  "tool": "list_all_external_tools",
  "parameters": {
    "useSemanticKernel": true
  }
}
```

### Call External Tool

```json
{
  "tool": "call_external_tool",
  "parameters": {
    "serverName": "Sequential Thinking",
    "toolName": "create_task_plan",
    "parameters": {
      "task": "Build a web application with user authentication"
    }
  }
}
```

## Semantic Search Features

The semantic search functionality provides intelligent tool discovery through:

### Vector Embeddings
- Each tool is represented as a vector embedding using Ollama's `mxbai-embed-large` model
- Embeddings capture semantic meaning of tool names and descriptions
- Enables finding tools based on functionality rather than exact name matches

### Similarity Scoring
- Uses cosine similarity to rank tool relevance
- Configurable minimum relevance threshold
- Returns ranked results with confidence scores

### Natural Language Queries
Examples of semantic search queries:
- "tools for analyzing code structure"
- "help me break down complex tasks"
- "find documentation or reference materials"
- "tools that work with language servers"

## Transport Protocols

### STDIO Transport
- Communicates with external processes via stdin/stdout
- Suitable for command-line tools and scripts
- Supports environment variable configuration

### HTTP Transport
- RESTful HTTP communication
- Supports both traditional HTTP and Server-Sent Events (SSE)
- Automatic transport mode detection

### Streamable HTTP
- Enhanced HTTP transport with streaming capabilities
- Optimized for real-time communication
- Fallback to SSE when streamable mode unavailable

## Monitoring and Logging

### Log Levels
- **Information**: Server startup, tool discovery, major operations
- **Debug**: Detailed operation traces, transport details
- **Warning**: Non-critical errors, fallback scenarios
- **Error**: Critical failures, service disruptions

### Metrics Endpoints
- `/health` - Server health status
- `/tool-index-info` - Tool index statistics and status

## Security Considerations

- **Process Isolation**: External MCP servers run in separate processes
- **Transport Security**: HTTP transports support secure connections
- **Input Validation**: All tool parameters are validated before forwarding
- **Resource Management**: Configurable timeouts and session limits

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement your changes
4. Add tests for new functionality
5. Submit a pull request

## System Requirements

- **.NET 9.0** or later
- **Windows/Linux/macOS** (cross-platform)
- **Minimum 2GB RAM** (more for large tool indexes)
- **Ollama** for semantic search features
- **Network access** for HTTP-based MCP servers

## Troubleshooting

### Common Issues

1. **Ollama Connection Failed**
   - Ensure Ollama is running: `ollama serve`
   - Verify the model is available: `ollama list`
   - Check the base URL in configuration

2. **MCP Server Won't Start**
   - Check command paths and arguments
   - Verify required dependencies (Node.js, Python, etc.)
   - Review log output for specific errors

3. **Tools Not Discovered**
   - Ensure MCP servers are running and responding
   - Check tool discovery logs
   - Try refreshing the tool index manually

4. **Semantic Search Not Working**
   - Verify Ollama is running and accessible
   - Check embedding model is downloaded
   - Ensure tool index is built successfully

## Acknowledgments

- **Model Context Protocol**: https://github.com/modelcontextprotocol
- **Microsoft Semantic Kernel**: https://github.com/microsoft/semantic-kernel
- **Ollama**: https://ollama.ai/
- **MCP Community**: For various external server implementations