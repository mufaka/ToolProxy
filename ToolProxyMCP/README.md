# ToolProxy MCP Server

A minimal Model Context Protocol (MCP) server that fronts a curated fleet of upstream MCP servers. Tool discovery is handled by the host's **Agent Skills** primitive — ToolProxy ships hand-authored `SKILL.md` files (one or more per upstream server) and an `install_skills` tool that copies them into a project's skills directory. The host loads each skill's description into base context and lazy-loads the body when a request matches.

This replaces an earlier embedding-based "semantic tool search" design that proved unreliable for narrowing the right tool out of a large fleet.

## Why this shape

Per-tool context bloat (Serena, in particular, ships verbose usage guidance with every tool description) dominates the agent's context budget at `tools/list` time, even before any tool fires. Skills push that guidance out of the always-loaded surface: descriptions stay short and lazy-loaded bodies hold the heavy prose. The host already knows how to load skills lazily, so ToolProxy doesn't need to mutate the MCP tool list at runtime — it just has to expose a small dispatch surface and ship the skill files.

## MCP tool surface

The proxy exposes exactly three tools:

| Tool | Purpose |
|------|---------|
| `call_external_tool(server, tool, arguments)` | Dispatch to the named tool on the named upstream server. Skills repeat this envelope verbatim per example. |
| `install_skills(skills_root)` | Copy ToolProxy's bundled `SKILL.md` files into `<skills_root>/<skill-name>/`. Pass the full skills root path; the proxy is host-neutral and does not append `.claude/skills` itself. Overwrite is unconditional. |
| `list_servers()` | Returns `[{ name, description, tool_count }]` for configured upstream servers. Debugging convenience. |

Dispatch parameter names are short by design (`server`/`tool`/`arguments`, matching the MCP `tools/call` wire spec), since the wrapper cost is paid on every dispatch.

## Skills

The bundled skills live under `<repo>/skills/<skill-name>/SKILL.md` in source and ship to `<proxy-install-dir>/skills/` at build time (via a `<Content Include="..\skills\**\*" />` glob). On install, the entire skill subtree is copied to `<skills_root>/<skill-name>/` — directory name is canonical, frontmatter `name` is not parsed.

For multi-purpose servers, naming convention is `toolproxy-<server>-<group>` so each skill stays focused. Serena ships as four skills: `toolproxy-serena-explore`, `toolproxy-serena-edit`, `toolproxy-serena-memory`, `toolproxy-serena-session`.

**For Claude Code, install project-locally** by passing `<project_root>/.claude/skills` as `skills_root`. The first install of a project will create the directory; restart the Claude Code session once after that so live skill reload starts watching it. Subsequent installs in the same session are picked up without restart.

## Configuration

`appsettings.json` covers the proxy's HTTP transport and the upstream server roster:

```jsonc
{
  "McpServer": {
    "Host": "localhost",
    "Port": 3030,
    "IdleTimeoutMinutes": 120,
    "MaxIdleSessions": 100000,
    "Stateless": false
  },
  "McpServers": [
    {
      "Name": "Serena",
      "Description": "Uses the language server protocol to find definitions, references, and modify function bodies of code.",
      "Transport": "stdio",
      "Command": "uvx",
      "Args": [
        "--from", "git+https://github.com/oraios/serena",
        "serena-mcp-server",
        "--context", "desktop-app"
      ],
      "Enabled": true,
      "Tools": []
    }
  ],
  "Logging": {
    "LogLevel": { "Default": "Information" }
  }
}
```

Per-server fields:

- `Transport`: `stdio`, `http`, `streamable-http`, or `sse`.
- `Command` / `Args` / `Env`: STDIO transport.
- `Url`: HTTP transports (auto-detects streamable-HTTP vs SSE).
- `Enabled`: false skips the server.
- `Tools`: optional fallback tool list used only if upstream tool discovery fails. Leave empty in normal use; the proxy discovers tools at startup via `tools/list`.

## Endpoints

- `GET /health` — liveness check.
- `POST /mcp` — MCP streamable-HTTP endpoint.

## Running

```bash
cd ToolProxyMCP
dotnet run
```

The proxy binds to the configured host/port (default `http://localhost:3030`) and starts the configured upstream servers on demand.

## Scope

In scope: dispatch, skill installation, basic introspection, in-house personal use against a small curated fleet (~5-10 servers), minimal external dependencies.

Out of scope: persistent vector store, authentication, multi-tenant concerns, hot-reload of upstream config, liveness probes, automated test coverage backfill. The previous Ollama / Semantic Kernel / `Microsoft.Extensions.AI` dependencies have been removed; skills are hand-authored and require no LLM at generation time.
