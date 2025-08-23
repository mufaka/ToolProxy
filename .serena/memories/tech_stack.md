# ToolProxy Tech Stack

## Core Framework
- **.NET 9**: Target framework
- **C# 13.0**: Language version with modern features
- **Console Application**: OutputType with hosted services

## Key Dependencies
- **Microsoft.Extensions.Configuration (9.0.8)**: Configuration management
  - Configuration.Binder: Model binding
  - Configuration.CommandLine: Command line arguments
  - Configuration.EnvironmentVariables: Environment variable support
  - Configuration.Json: JSON configuration files
- **Microsoft.Extensions.DependencyInjection (9.0.8)**: Dependency injection container
- **Microsoft.Extensions.Logging (9.0.8)**: Structured logging
  - Logging.Console: Console logging provider
- **ModelContextProtocol (0.3.0-preview.4)**: Core MCP functionality

## Language Features Used
- **Nullable Reference Types**: Enabled for better null safety
- **Implicit Usings**: Enabled to reduce boilerplate
- **Modern C# Patterns**: 
  - Pattern matching in switch expressions
  - Null-conditional operators (?.)
  - Null-coalescing operators (??)
  - String interpolation with structured logging
  - Async/await throughout
  - Using declarations for disposables

## Development Tools
- **Visual Studio**: Primary IDE (evident from .vs folder)
- **Git**: Version control (standard for .NET projects)
- **NuGet**: Package management