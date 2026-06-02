# ToolProxy

A Model Context Protocol (MCP) server that fronts a curated fleet of upstream MCP servers and uses the host's **Agent Skills** primitive for progressive disclosure of per-server guidance.

## Solution layout

- **[ToolProxyMCP](ToolProxyMCP/README.md)** — the active MCP server. Three tools: `call_external_tool`, `install_skills`, `list_servers`. Ships hand-authored `SKILL.md` files that the agent's host loads lazily.
- **ToolProxy.Chat** — Avalonia desktop chat client. **Deprecated.** Stays in the repo for historical reference and as a possible future home for first-class skill support; receives no changes.

## How it works

1. The proxy connects to each configured upstream MCP server at startup and discovers their tools.
2. The agent calls `install_skills(skills_root)` once per project — typically with `<project_root>/.claude/skills` for Claude Code project-local install. The proxy copies bundled `toolproxy-*` skill directories into that location.
3. The host loads each skill's short description into base context. When a user request matches a skill, the host lazy-loads its body, which contains the operating principles, tool reference, and worked examples for that upstream server.
4. The agent dispatches tool calls through `call_external_tool(server, tool, arguments)`. Each skill body shows the exact envelope to use.

This replaces an earlier embedding-based "semantic tool search" design. The host agent's tool selection is more reliable than cosine retrieval, and skills give us native progressive disclosure without depending on `tools/list_changed` (which has inconsistent client support).

## Quick start

```bash
cd ToolProxyMCP
dotnet run
```

Then, from your MCP-capable host (e.g. Claude Code), connect to `http://localhost:3030/mcp` and ask the agent to install ToolProxy's skills:

> Install ToolProxy's skills into this project.

The agent will call `install_skills` with `<project_root>/.claude/skills`. On first install in a new project, restart the host once so live skill reload starts watching the directory.

## Bundled skills

Source: `<repo>/skills/<skill-name>/SKILL.md`. Currently:

- `toolproxy-serena-explore` — code navigation (find symbols, references)
- `toolproxy-serena-edit` — symbol-level code edits
- `toolproxy-serena-memory` — Serena's memory store
- `toolproxy-serena-session` — onboarding / session-level concerns
- `toolproxy-ms-docs` — Microsoft Learn documentation lookup (search, fetch, code samples)
- `toolproxy-context7` — third-party library documentation lookup via Context7
- `toolproxy-teamware-projects` — Teamware project discovery, activity, and lounge coordination
- `toolproxy-teamware-tasks` — Teamware task triage, creation, assignment, comments, and workflow updates
- `toolproxy-teamware-ideas` — Teamware idea discovery, specification context, and idea discussion
- `toolproxy-teamware-inbox` — Teamware inbox capture and inbox-to-task processing
- `toolproxy-srclight-workspace` — Srclight workspace maps, index state, build targets, and platform-conditionals
- `toolproxy-srclight-explore` — Srclight symbol search, file symbol listings, signatures, and full symbol reads
- `toolproxy-srclight-impact` — Srclight callers, dependencies, hierarchies, tests, and platform variants
- `toolproxy-srclight-history` — Srclight recent changes, hotspots, blame, and uncommitted work
- `toolproxy-sqltools-schema` — SqlTools SQL Server connections, table metadata, and stored procedure definitions
- `toolproxy-sqltools-query` — SqlTools read-only query validation, execution, and top-row sampling
- `toolproxy-sqltools-catalog` — SqlTools C# data-access cataloging and stored-procedure usage discovery
- `toolproxy-gitnexus` — GitNexus code intelligence: execution-flow search, symbol context, blast-radius impact, git-diff change mapping, and call-graph-aware rename

Skills are hand-authored. To change what a project sees, edit the master `SKILL.md` under `<repo>/skills/` and re-run `install_skills`.

## Prerequisites

- .NET 10 SDK
- Whatever runtimes the configured upstream servers need (commonly Node.js for `npx`-based servers, Python with `uvx` for Serena).

## Project structure

```
ToolProxy/
├── ToolProxyMCP/                # Active MCP server
│   ├── Configuration/           # appsettings binding
│   ├── Services/                # McpManager, McpDispatcher, hosted service
│   ├── Tools/                   # LocalTool, SkillsInstallTool
│   ├── appsettings.json         # Upstream server roster
│   └── README.md                # Server-side details
├── ToolProxy.Chat/              # Deprecated desktop client (kept for reference)
├── skills/                      # Master skill source — bundled at build time
│   └── toolproxy-serena-*/SKILL.md
├── ToolProxyRefactorIdea.md     # Design rationale
├── ToolProxyRefactorPlan.md     # Implementation plan / change log
└── README.md                    # This file
```

## Related

- [Model Context Protocol](https://github.com/modelcontextprotocol)
- [Agent Skills](https://agentskills.io)
