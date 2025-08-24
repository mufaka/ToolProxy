# ToolProxy - MCP Server with Semantic Kernel Integration

A sophisticated Model Context Protocol (MCP) server that acts as a proxy and aggregator for multiple external MCP servers, enhanced with AI-powered semantic search capabilities using Microsoft Semantic Kernel and Ollama embeddings.

## Features

- **Multi-Server MCP Proxy**: Connect to and manage multiple external MCP servers simultaneously
- **Semantic Tool Search**: AI-powered tool discovery using vector embeddings and natural language queries
- **Multiple Transport Support**: STDIO, HTTP, and SSE transport protocols
- **Tool Discovery**: Automatic discovery and indexing of tools from connected MCP servers
- **REST API**: HTTP endpoints for health checks and tool information
- **Real-time Tool Refresh**: Dynamic updating of tool indexes without server restart
- **Vector-Based Search**: Semantic similarity search powered by Ollama embeddings

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
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "ModelName": "mxbai-embed-large"
    }
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
    },
    {
      "Name": "Sequential Thinking",
      "Description": "Reduces complex tasks into simpler steps, reasons about plans",
      "Transport": "stdio",
      "Command": "cmd.exe",
      "Args": [ "/c", "npx", "-y", "@modelcontextprotocol/server-sequential-thinking" ],
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
   ollama pull mxbai-embed-large # or nomic-embed-text for larger context
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