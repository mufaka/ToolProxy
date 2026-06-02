# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ToolProxy is an MCP server that fronts a curated fleet of *upstream* MCP servers and uses the host's **Agent Skills** primitive for progressive disclosure of per-server guidance. It exposes a tiny, fixed tool surface (three tools) and ships hand-authored `SKILL.md` files; the host loads each skill's short description into base context and lazy-loads the body only when a request matches.

This deliberately replaced an earlier embedding/Ollama "semantic tool search" design. The host's tool selection on skill descriptions proved more reliable than cosine retrieval, so the proxy no longer mutates the MCP tool list at runtime — it just dispatches and ships skill files. Do not reintroduce vector stores, LLM-at-runtime narrowing, or `tools/list_changed` dependence.

## Commands

```powershell
# Build / run (the active project is ToolProxyMCP; target is net10.0)
cd ToolProxyMCP
dotnet run                 # binds http://localhost:3030 by default
dotnet run -- --debug      # verbose logging (args flow through AddCommandLine)
dotnet build               # from repo root builds the whole solution
dotnet build --configuration Release
```

There are **no automated tests** — test coverage is explicitly out of scope. Verify changes by running the proxy and exercising it from an MCP host. Health check: `GET /health`. MCP endpoint: `POST /mcp`.

`ToolProxy.Chat` is a **deprecated** Avalonia desktop client (targets net9.0) kept only for historical reference. Do not make changes there unless explicitly asked.

## Architecture

Request flow for an upstream tool call:

1. **Startup** (`McpHostedService` → `McpManager.StartAllServersAsync`): the proxy reads the `McpServers` roster from `appsettings.json`, creates one `ManagedMcpServer` per entry, starts the enabled ones, and discovers each upstream server's tools via `tools/list`.
2. **Skill install** (`SkillsInstallTool.install_skills`): the agent calls this once per project with a full `skills_root` path; the proxy copies each bundled `skills/<name>/` subtree into it.
3. **Dispatch** (`LocalTool.call_external_tool` → `McpDispatcher` → `ManagedMcpServer.CallToolAsync`): the agent, guided by a loaded skill, sends `{server, tool, arguments}`; the proxy looks up the named server (case-insensitive in code, but skills must match `appsettings.json` casing — see below) and forwards the call through the official `ModelContextProtocol` client SDK.

Key types:
- `ManagedMcpServer` (`Services/`) — owns one upstream connection. Builds the client transport (`stdio` / `http` / `streamable-http` / `sse`; HTTP variants auto-detect streamable-vs-SSE), discovers tools, and translates `JsonElement` arguments into the SDK call. `CallToolAsync` validates the tool name against discovered tools and returns only the concatenated `text` content blocks.
- `McpManager` — registry of all `ManagedMcpServer`s, keyed case-insensitively; lifecycle (start/stop/refresh) fan-out.
- `LocalTool` / `SkillsInstallTool` (`Tools/`) — the three `[McpServerTool]` methods. `call_external_tool`, `list_servers`, and `install_skills` are the *entire* public surface. `list_servers` and `install_skills` are top-level proxy meta-tools and must NOT be routed through `call_external_tool` (the tool descriptions say so explicitly — preserve that).
- `Program.cs` — ASP.NET host wiring: config (json + env + command line), DI singletons, `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`.

Config classes live in `Configuration/` (`AppSettings`, `McpServerConfig`, `LoggingSettings`). `McpServerConfig.Tools` is only a fallback list used if live `tools/list` discovery fails; leave it empty normally.

Note: `Program.cs` defaults `McpServer:Stateless` to `true` if the key is absent, but the shipped `appsettings.json` sets it to `false`. Don't assume the default — read the config.

## Skills (the load-bearing part of the design)

- **Source of truth:** `<repo>/skills/<skill-name>/SKILL.md`. Hand-authored markdown + YAML frontmatter (`name`, `description` only).
- **Bundling:** `ToolProxy.csproj` has a `<Content Include="..\skills\**\*" ... Link="skills\..." />` glob, so anything under `<repo>/skills/` ships to `<output>/skills/` automatically — **no csproj edit needed** to add a skill. `install_skills` copies from `AppContext.BaseDirectory/skills`, i.e. the build output, so you must rebuild after editing a skill before re-installing.
- **There are two copies in the repo.** `<repo>/skills/` is canonical; `<repo>/.claude/skills/` is an installed copy for this project. Edit the canonical source, rebuild, then re-run `install_skills` — don't hand-edit only the `.claude/skills` copy.
- **Install semantics:** directory name is canonical (frontmatter `name` is not parsed for placement); copy is unconditional/overwrite. First install into a fresh `skills_root` requires one host restart so live reload starts watching.

When authoring or editing skills, follow the detailed house style in `.serena/memories/skill_authoring.md` and use the existing `skills/toolproxy-*` files as templates. The non-obvious rules that the system depends on:

- **The `description` is everything** — it's the only text in the host's base context and drives whether the skill fires. Pack it with concrete capabilities, the *casual phrasings users actually type*, and an explicit "prefer over the host's Read/Edit/memory" comparison when the skill should win over a built-in. A vague description silently regresses to host defaults.
- **Every example must show the full `{server, tool, arguments}` envelope** — bodies are loaded in isolation and can't infer structure from context.
- **Server names in skill examples are case-sensitive and must exactly match `appsettings.json`** (`"Serena"`, `"context7"`, `"Playwright"`, `"Clear Thought"`).
- Naming is `toolproxy-<server>-<surface>`; split a multi-purpose server into focused per-surface skills (Serena → explore/edit/memory/session).
- Spend body length on **workflows/sequencing**, not on duplicating the live tool JSONSchema.

For authoring tasks, the `toolproxy-skill-author` skill encodes this workflow end-to-end.

## Upstream runtime prerequisites

Whatever the configured upstream servers need: Node.js for `npx`-based servers (context7, Playwright, Clear Thought), and Python with `uvx` for Serena. Disabling a server is just `"Enabled": false` in its roster entry.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **ToolProxy** (689 symbols, 1057 relationships, 17 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/ToolProxy/context` | Codebase overview, check index freshness |
| `gitnexus://repo/ToolProxy/clusters` | All functional areas |
| `gitnexus://repo/ToolProxy/processes` | All execution flows |
| `gitnexus://repo/ToolProxy/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
