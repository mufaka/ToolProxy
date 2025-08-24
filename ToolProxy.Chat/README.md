# ToolProxy Chat

A modern desktop chat application built with Avalonia UI that integrates with the ToolProxy MCP (Model Context Protocol) server and local language models via Ollama.

## Overview

ToolProxy Chat provides an intuitive chat interface that allows users to interact with AI agents that have access to various tools through the ToolProxy MCP server. The application leverages Microsoft's Semantic Kernel framework for agent orchestration and Ollama for local language model inference.

## Features

- **Modern UI**: Built with Avalonia UI for cross-platform desktop support (.NET 9)
- **AI Agent Integration**: Powered by Microsoft Semantic Kernel with intelligent tool selection
- **Local LLM Support**: Uses Ollama for privacy-focused, local language model inference
- **Tool Integration**: Seamlessly connects to ToolProxy MCP server for extended capabilities
- **Real-time Chat**: Responsive chat interface with message history
- **Configurable**: Easy configuration through JSON settings

## Architecture

### Core Components

- **MainWindow**: Primary chat interface with message display and input
- **KernelAgentService**: Manages Semantic Kernel agent and MCP client connections
- **ChatConfiguration**: Configuration model for Ollama, ToolProxy, and agent settings
- **MainWindowViewModel**: MVVM pattern implementation for UI data binding

### Dependencies

- **Avalonia UI 11.1.3**: Cross-platform UI framework
- **Microsoft Semantic Kernel 1.63.0**: AI orchestration and agent framework
- **Ollama Connector**: Local language model integration
- **Model Context Protocol (MCP)**: Tool integration protocol
- **Microsoft Extensions**: Configuration, DI, and hosting

## Configuration

The application is configured through `appsettings.json`:

```json
{
  "Chat": {
    "Ollama": {
      "BaseUrl": "http://localhost:11434/v1",
      "ModelName": "qwen2.5:7b-instruct",
      "Temperature": 0.7
    },
    "ToolProxy": {
      "BaseUrl": "http://localhost:3030",
      "McpEndpoint": "/mcp"
    },
    "Agent": {
      "SystemPrompt": "You are a helpful AI assistant..."
    }
  }
}
```

### Configuration Options

- **Ollama**: Configure local LLM endpoint, model, and generation parameters
- **ToolProxy**: Set MCP server connection details
- **Agent**: Customize system prompt and agent behavior

## Prerequisites

1. **Ollama**: Install and run Ollama with your preferred language model
2. **ToolProxy Server**: Ensure the ToolProxy MCP server is running
3. **.NET 9**: Required runtime for the application

## Getting Started

1. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd ToolProxy.Chat
   dotnet build
   ```

2. **Configure Settings**:
   - Update `appsettings.json` with your Ollama and ToolProxy server URLs
   - Customize the agent system prompt as needed

3. **Run the Application**:
   ```bash
   dotnet run
   ```

## Usage

1. **Start Chat**: Type messages in the input box and press Enter to send
2. **Tool Access**: The AI agent automatically discovers and uses available tools from ToolProxy
3. **Clear History**: Use the "Clear History" button to reset the conversation
4. **Connection Status**: Monitor the connection indicator in the status bar

## Development

### Project Structure

```
ToolProxy.Chat/
├── Models/          # Data models and configuration
├── Services/        # Business logic and external integrations
├── ViewModels/      # MVVM view models
├── Views/           # Avalonia UI views
├── App.axaml        # Application configuration
└── Program.cs       # Application entry point
```

### Key Services

- **IKernelAgentService**: Interface for AI agent operations
- **KernelAgentService**: Implementation handling Semantic Kernel and MCP integration

### MVVM Pattern

The application follows MVVM principles with:
- **Models**: Data structures and configuration
- **Views**: AXAML markup for UI
- **ViewModels**: Business logic and data binding

## Contributing

1. Follow the existing code patterns and MVVM architecture
2. Update configuration models when adding new settings
3. Ensure proper error handling and logging
4. Test with different Ollama models and ToolProxy configurations

## Related Projects

- **ToolProxy**: The main MCP server providing tool capabilities
- **Ollama**: Local language model inference engine
- **Semantic Kernel**: Microsoft's AI orchestration framework