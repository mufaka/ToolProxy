# ToolProxy Project Overview

## Purpose
ToolProxy fronts a curated fleet of upstream MCP (Model Context Protocol) servers behind a single MCP endpoint, and uses the host agent's **Agent Skills** primitive for progressive disclosure of per-server guidance. This replaced an earlier embedding-based "semantic tool search" design that didn't match user intent reliably.

In-house personal use against ~5–10 servers; not multi-tenant, no auth, no persistence, no hot reload.

## Tech stack
- .NET 10 (`net10.0`)
- ASP.NET Core hosting (`WebApplication.CreateBuilder`) — the proxy is a web app, not a console app
- `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` 1.2.0
- Default MCP endpoint: streamable HTTP at `http://localhost:3030/mcp` (host/port configurable via `McpServer` in appsettings.json)

## Top-level tools (the proxy's own MCP surface)
Exactly three. These are the proxy's own tools, exposed directly on this MCP server — they are NOT routed through `call_external_tool`:
- **`call_external_tool(server, tool, arguments)`** — dispatch a call to a tool advertised by a configured upstream server.
- **`list_servers()`** — list configured upstream servers with descriptions and tool counts.
- **`install_skills(skills_root)`** — copy bundled `toolproxy-*` skill directories to the host's skills root (typically `<project>/.claude/skills`).

## Architecture
- **Services** (`ToolProxyMCP/Services/`)
  - `IMcpManager` / `McpManager` — lifecycle of all upstream servers; owns the `_servers` dictionary (case-insensitive keys as of 2026-04-29).
  - `IManagedMcpServer` / `ManagedMcpServer` — wraps a single upstream MCP connection.
  - `IMcpDispatcher` / `McpDispatcher` — thin routing layer for `call_external_tool`. Single method: `CallExternalToolAsync(server, tool, arguments, ct)`.
  - `McpHostedService` — hooks lifecycle into `IHostedService`.
- **Tools** (`ToolProxyMCP/Tools/`)
  - `LocalTool` — `[McpServerToolType]` exposing `call_external_tool` and `list_servers`.
  - `SkillsInstallTool` — `[McpServerToolType]` exposing `install_skills`.
- **Configuration** — `AppSettings` binds `appsettings.json` (`McpServer` block + `McpServers` array).
- **Skills payload** — source at `<repo>/skills/<name>/SKILL.md`, bundled into the build via a `<Content Include="..\skills\**\*" .../>` glob in `ToolProxyMCP/ToolProxy.csproj`. `install_skills` copies the tree at runtime.

## Configured upstream servers (current `appsettings.json`)
- **context7** — library documentation (stdio)
- **Playwright** — browser automation (stdio)
- **Serena** — LSP-backed symbol-aware code intelligence (stdio, `uvx`)
- **Clear Thought** — systematic-thinking / mental-models (stdio)

Server-name lookup is **case-insensitive** in the dispatcher; the original casing is preserved in `list_servers` output and logs.

## Solution layout
- **`ToolProxyMCP/`** — the active server. This is what runs.
- **`ToolProxy.Chat/`** — Avalonia desktop chat client. **Deprecated** — kept for historical reference, still on `net9.0` and the older MCP SDK, not maintained, not in scope for the refactor.
- **`skills/`** — master `SKILL.md` source for the bundled `toolproxy-*` skills.
- **`ToolProxyRefactorIdea.md`** — design rationale.
- **`ToolProxyRefactorPlan.md`** — implementation plan / change log.

## Out of scope (will not be reintroduced)
- Authentication, persistent storage, hot-reload of server config, liveness probes.
- Cloud LLMs, external vector DBs, heavyweight ML stack.
- Embedding-based tool narrowing (Ollama-local + Semantic Kernel + vector data) — explicitly removed; the skills-based design is the replacement.
