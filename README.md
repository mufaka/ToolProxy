# ToolProxy - AI-Powered Tool Discovery and Chat Interface

A comprehensive solution for intelligent tool discovery and AI-powered chat interactions using the Model Context Protocol (MCP) and local language models.

## Overview

This solution consists of two interconnected components that work together to provide an enhanced AI assistant experience with access to a wide variety of tools:

<img width="1992" height="1503" alt="image" src="https://github.com/user-attachments/assets/d503543e-bdd8-4821-a2f3-4d465c43aecb" />

### 🛠️ [ToolProxy MCP Server](ToolProxyMCP/README.md)
A sophisticated MCP server that acts as a proxy and aggregator for multiple external MCP servers, enhanced with AI-powered semantic search capabilities.

**Key Features:**
- **Multi-Server Aggregation**: Connect to multiple external MCP servers simultaneously
- **Semantic Tool Discovery**: AI-powered tool search using vector embeddings and natural language queries
- **Multiple Transport Protocols**: STDIO, HTTP, and SSE support
- **Real-time Tool Indexing**: Dynamic tool discovery and refresh capabilities
- **Vector-Based Search**: Powered by Ollama embeddings for intelligent tool matching

### 💬 [ToolProxy Chat](ToolProxy.Chat/README.md)
A modern desktop chat application built with Avalonia UI that provides an intuitive interface for interacting with AI agents that have access to tools through the ToolProxy MCP server.

**Key Features:**
- **Modern Cross-Platform UI**: Built with Avalonia UI for Windows, macOS, and Linux
- **AI Agent Integration**: Powered by Microsoft Semantic Kernel with intelligent tool selection
- **Local LLM Support**: Uses Ollama for privacy-focused, local language model inference
- **Real-time Chat**: Responsive chat interface with message history and status indicators

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     ToolProxy Solution                      │
├─────────────────────────────────────────────────────────────┤
│  ToolProxy.Chat (Desktop Application)                       │
│  ├─ Avalonia UI Frontend                                    │
│  ├─ Semantic Kernel Agent                                   │
│  └─ MCP Client Integration                                  │
├─────────────────────────────────────────────────────────────┤
│                         MCP Protocol                        │
├─────────────────────────────────────────────────────────────┤
│  ToolProxy MCP Server                                       │
│  ├─ Semantic Tool Search                                    │
│  ├─ Multi-Server Proxy                                      │
│  ├─ Vector Store & Embeddings                               │
│  └─ External MCP Server Management                          │
├─────────────────────────────────────────────────────────────┤
│  External MCP Servers                                       │
│  ├─ Context7 (Documentation)                                │
│  ├─ Sequential Thinking                                     │
│  ├─ Serena (Language Server)                                │
│  └─ Custom Servers...                                       │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

### Prerequisites
- **.NET 9 SDK**
- **Ollama** (for local LLM and embeddings)
- **Node.js** (for npm-based MCP servers)

### 1. Start Ollama
```bash
ollama serve
ollama pull qwen2.5:7b-instruct    # For chat
ollama pull mxbai-embed-large      # For semantic search, or nomic-embed-text for larger context size
```

### 2. Start ToolProxy MCP Server
```bash
cd ToolProxyMCP
dotnet run
```

### 3. Start ToolProxy Chat Application
```bash
cd ToolProxy.Chat
dotnet run
```

## How It Works

1. **Tool Discovery**: The ToolProxy MCP server discovers and indexes tools from multiple external MCP servers
2. **Semantic Search**: Tools are embedded as vectors using Ollama, enabling natural language search
3. **Chat Interface**: Users interact through the desktop chat application
4. **Intelligent Tool Selection**: The AI agent automatically searches for and uses relevant tools based on user requests
5. **Tool Execution**: Tools are executed through the MCP protocol and results are returned to the chat

## Example Workflow

1. User asks: *"Can you help me analyze this code repository?"*
2. ToolProxy Chat agent searches for relevant tools using semantic search
3. Agent discovers code analysis tools from connected MCP servers
4. Agent uses the appropriate tools to analyze the repository
5. Results are presented in the chat interface

## Configuration

Both applications share similar configuration patterns:

- **Ollama Settings**: Configure LLM endpoints and models
- **MCP Server Settings**: Define external server connections
- **Agent Behavior**: Customize system prompts and behavior

See individual project READMEs for detailed configuration options.

## Use Cases

- **Development Assistance**: Code analysis, documentation lookup, project management
- **Research and Documentation**: Access to up-to-date library documentation and references
- **Task Planning**: Complex task decomposition and sequential thinking
- **Multi-Tool Workflows**: Combining multiple specialized tools in intelligent workflows

## Contributing

1. Fork the repository
2. Create feature branches for each component
3. Follow the existing patterns and architecture
4. Add tests for new functionality
5. Update documentation as needed

## Project Structure

```
ToolProxy/
├── ToolProxyMCP/           # MCP Server with semantic search
│   ├── Services/           # Core services and MCP management
│   ├── Tools/              # Built-in tools and extensions
│   ├── Models/             # Data models and configuration
│   └── README.md           # Detailed server documentation
├── ToolProxy.Chat/         # Desktop chat application
│   ├── Views/              # Avalonia UI views
│   ├── ViewModels/         # MVVM view models
│   ├── Services/           # Business logic and integrations
│   ├── Models/             # Data models and configuration
│   └── README.md           # Detailed chat app documentation
└── README.md               # This file
```

## Technology Stack

- **.NET 9**: Modern cross-platform runtime
- **Microsoft Semantic Kernel**: AI orchestration and agent framework
- **Avalonia UI**: Cross-platform desktop UI framework
- **Model Context Protocol**: Tool integration standard
- **Ollama**: Local language model inference
- **Vector Embeddings**: Semantic search capabilities

## Related Projects

- **Model Context Protocol**: https://github.com/modelcontextprotocol
- **Microsoft Semantic Kernel**: https://github.com/microsoft/semantic-kernel
- **Ollama**: https://ollama.ai/
- **Avalonia UI**: https://avaloniaui.net/
