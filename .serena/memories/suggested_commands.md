# ToolProxy Suggested Commands

## Development Commands

### Build and Run
```powershell
# Build the project
dotnet build

# Run the application
dotnet run

# Run with debug logging
dotnet run -- --debug

# Build for release
dotnet build --configuration Release

# Run release build
dotnet run --configuration Release
```

### Package Management
```powershell
# Restore NuGet packages
dotnet restore

# Add a new package
dotnet add package <PackageName>

# Update packages
dotnet update
```

### Project Information
```powershell
# Show project info
dotnet list package

# Show outdated packages
dotnet list package --outdated

# Check for vulnerabilities
dotnet list package --vulnerable
```

### Windows Development Tools
```powershell
# List files (Windows equivalent of ls)
dir
Get-ChildItem

# Find files
Get-ChildItem -Recurse -Filter "*.cs"

# Search in files (Windows equivalent of grep)
Select-String "pattern" -Path "*.cs" -Recurse

# Navigate directories
cd <directory>
Set-Location <directory>

# Git commands
git status
git add .
git commit -m "message"
git push
```

### Configuration
- Edit `appsettings.json` to configure MCP servers
- Set environment variables for server-specific configuration
- Use `--debug` command line argument for verbose logging

### Dependencies
- **Ollama**: Required for ask-ollama server (configure OLLAMA_URL)
- **SQL Server**: Required for sql-stored-procedures server
- **Node.js/NPX**: Required for context7 and sequential-thinking servers (disabled by default)
- **Python/UV**: Required for Serena server (disabled by default)