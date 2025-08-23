# ToolProxy Code Style and Conventions

## General C# Conventions
- **Naming**: PascalCase for public members, camelCase for private fields with underscore prefix
- **Interfaces**: Prefixed with 'I' (IMcpManager, IManagedMcpServer)
- **Async Methods**: Suffixed with 'Async'
- **Nullable Reference Types**: Enabled - use proper nullable annotations

## File Organization
- **Namespaces**: Match folder structure (ToolProxy.Services, ToolProxy.Configuration)
- **One Class Per File**: Standard practice followed
- **Interface Segregation**: Separate interface files from implementations

## Coding Patterns
- **Dependency Injection**: Constructor injection throughout
- **Async/Await**: All I/O operations are async with CancellationToken support
- **Disposal Pattern**: Proper IDisposable implementation with disposal tracking
- **Logging**: Structured logging with Microsoft.Extensions.Logging
- **Configuration**: Strongly-typed settings classes bound from appsettings.json

## Error Handling
- **ArgumentNullException**: Thrown for null constructor parameters
- **Try-Catch**: Used around external service calls with logging
- **Graceful Degradation**: Services continue running even if individual servers fail

## Example Patterns Used
```csharp
// Constructor validation
_settings = settings ?? throw new ArgumentNullException(nameof(settings));

// Async with cancellation
public async Task<bool> StartAsync(CancellationToken cancellationToken = default)

// Structured logging
_logger.LogInformation("Started {SuccessCount}/{TotalCount} MCP servers", successCount, totalEnabled);

// Proper disposal
public void Dispose()
{
    if (!_disposed)
    {
        // cleanup logic
        _disposed = true;
    }
}
```