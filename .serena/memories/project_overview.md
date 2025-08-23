# ToolProxy Project Overview

## Purpose
ToolProxy is a .NET 9 console application that acts as a proxy and manager for multiple MCP (Model Context Protocol) servers. It allows users to configure, start, stop, and manage various MCP servers through a unified interface.

## Key Features
- Manages multiple MCP servers with different transport types (STDIO, HTTP, SSE)
- Supports configuration through appsettings.json
- Provides unified logging and error handling
- Supports both debug and production modes
- Includes tool discovery and refresh capabilities
- Properly handles server lifecycle management

## Architecture
- **Services Layer**: Contains IMcpManager and IManagedMcpServer interfaces with their implementations
- **Configuration Layer**: Handles settings binding from appsettings.json
- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection
- **Hosting**: Built on Microsoft.Extensions.Hosting framework
- **Logging**: Integrated Microsoft.Extensions.Logging

## Main Components
1. **McpManager**: Central manager for all MCP servers
2. **ManagedMcpServer**: Individual server management and proxy functionality
3. **AppSettings**: Configuration model binding
4. **Program**: Application entry point and service registration

## MCP Servers Configured
- **ask-ollama**: Ollama-based AI Chat using STDIO transport
- **sql-stored-procedures**: SQL Server database tools
- **context7**: Library documentation service (disabled)
- **Sequential Thinking**: Task reasoning service (disabled)  
- **Serena**: Language server protocol integration (disabled)
- **Project Pilot**: Project and task management (disabled, HTTP transport)