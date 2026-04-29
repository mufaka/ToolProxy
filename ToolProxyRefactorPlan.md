# ToolProxy Refactor — Implementation Plan

Trackable companion to `ToolProxyRefactorIdea.md`. The idea doc is the source of truth for *why*; this doc is the *how* and *what's done*. Phases are ordered to minimize churn — each phase should leave the build green.

Mark items `[x]` as completed. Add notes inline under a task when implementation reveals something the idea doc didn't cover (then sync to the idea doc if it's a real decision).

---

## Phase 1 — .NET 10 + dependency cleanup (prerequisite)

Isolate dependency churn from logic changes. Build must be green before Phase 2.

- [x] Bump TFM `net9.0` → `net10.0` in `ToolProxyMCP/ToolProxy.csproj` (and any other projects participating in the refactor).
  - ToolProxy.Chat left on `net9.0` per its deprecated status; not in the refactor scope.
- [x] Update SDK in `global.json` if pinned. (No `global.json` exists; SDK 10.0.105 is on PATH.)
- [x] Update `ModelContextProtocol` SDK from `0.3.0-preview.4` to current (`1.2.0`); resolve breaking changes.
  - Renames in 1.x: `IMcpClient` → `McpClient`; `SseClientTransport`/`SseClientTransportOptions` → `HttpClientTransport`/`HttpClientTransportOptions`; `McpClientFactory.CreateAsync` → `McpClient.CreateAsync`. Updated `Services/ManagedMcpServer.cs`.
  - `Microsoft.Extensions.VectorData.Abstractions` 10.x: `[VectorStoreVector]` `Dimensions` is now positional, not named. Updated `Models/ToolVectorRecord.cs` (file is being deleted in Phase 2 anyway).
- [ ] ~~Remove NuGet refs: `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Connectors.InMemory`, `OllamaSharp`.~~ Deferred to Phase 2 — removing the packages without deleting their consumers breaks the build, so the package removal is folded into Phase 2's atomic strip-and-delete step. (The plan's Phase 1 smoke-test note also says "legacy semantic tool is still wired up at this point" — consistent with that interpretation.)
- [ ] Remove `Microsoft.Extensions.AI` if no remaining usage after Phase 2.
- [x] `dotnet build` clean across the solution. ToolProxy.Chat builds on `net9.0` against the old MCP SDK (acceptable per scope).
- [ ] Smoke test: launch the proxy with one upstream server and verify `tools/list` still works (legacy semantic tool is still wired up at this point — that's fine).

## Phase 2 — Strip semantic search

Delete the embedding/index path entirely. Leaves dispatch + upstream-server plumbing intact.

- [x] Delete `ToolProxyMCP/Tools/EnhancedLocalTool.cs` (the `search_tools_semantic` tool and friends).
- [x] Delete `ToolProxyMCP/Services/SemanticKernelToolIndexService.cs`.
- [x] Delete `ToolProxyMCP/Services/EnhancedSemanticKernelToolIndexService.cs`.
- [x] Delete `ToolProxyMCP/Models/ToolVectorRecord.cs`. (Empty `Models/` folder also removed.)
- [x] Delete `ToolProxyMCP/Configuration/SemanticKernelSettings.cs`.
- [x] Strip embedding/phrase-rewriting blocks from `appsettings.json` (no `appsettings.Development.json` exists).
- [x] In `Program.cs`, remove DI registrations for the deleted services and any `Microsoft.Extensions.AI` / Ollama wiring. Also dropped the `/search-tools` and `/tool-index-info` HTTP endpoints and the `SearchToolsRequest` record. `McpHostedService` no longer takes `IToolIndexService` and no longer triggers an index refresh on startup.
- [x] **Reshape the dispatcher service.** Renamed `IToolIndexService` → `IMcpDispatcher` and the impl → `McpDispatcher`. Trimmed to a single `CallExternalToolAsync` method; dropped the cache, search, and refresh members. `list_all_servers_and_tools_json` reads directly from `IMcpManager`.
- [x] Re-home the surviving local tools into `Tools/LocalTool.cs` (`call_external_tool` and `list_all_servers_and_tools_json`).
- [x] Also dropped `IndexManualTests/` (the `.http` files were aimed at the removed `/search-tools` endpoint).
- [x] Removed package refs from `ToolProxy.csproj`: `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Connectors.InMemory`, `Microsoft.SemanticKernel.Connectors.Ollama`, `OllamaSharp`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.VectorData.Abstractions`, `System.Linq.Async`.
- [x] `dotnet build` clean (zero warnings, zero errors).

## Phase 3 — Rename dispatch parameters

Pin the `server` / `tool` / `arguments` shape that the skill drafts already use.

- [x] In the dispatcher tool method, rename: `serverName` → `server`, `toolName` → `tool`, `parameters` → `arguments`. Update `[Description]` attributes accordingly.
- [x] Cascade renames through the dispatcher interface and implementation (`CallExternalToolAsync` parameter names).
- [x] Update the tool's top-level `[Description]` to reflect the new envelope (no references to "search results sample" — that path is gone).
- [ ] Confirm via `tools/list` against a live client that the parameter schema reads `server`, `tool`, `arguments`. *(Deferred to Phase 7 manual smoke test.)*
- [ ] Make one end-to-end dispatch call against an upstream server using the new shape (e.g., a Serena `check_onboarding_performed`). *(Deferred to Phase 7 manual smoke test.)*

## Phase 4 — Implement `install_skills` MCP tool

The agent-invoked installer.

- [x] Add `Tools/SkillsInstallTool.cs`.
- [x] Resolve the proxy's master skills directory: `Path.Combine(AppContext.BaseDirectory, "skills")`. Verified via a small driver project that `AppContext.BaseDirectory` lands at `bin/.../net10.0/` and the `skills/` subtree is visible there.
- [x] Implement `install_skills(skills_root)`:
  - [x] Validate `skills_root` is a non-empty absolute path; reject relative paths with a clear error. (Uses `Path.IsPathFullyQualified`.)
  - [x] Detect whether `skills_root` already exists (drives the first-install warning).
  - [x] Create `skills_root` if missing.
  - [x] For each subdirectory under `<proxy-install-dir>/skills/` containing a `SKILL.md`, copy the entire skill subtree to `<skills_root>/<skill-name>/`. Skill *names* are taken from the source directory name (frontmatter `name` not parsed). Overwrite unconditionally; no backup, no diff.
  - [x] Collect per-skill outcome: `{ name, path, status: "installed" | "error", error?: string }`.
  - [x] Return JSON: `{ skills_root, created_skills_root: bool, warning: string?, results: [...] }`. The first-install warning appears as a `warning` field (null on subsequent installs).
- [x] Register the tool with `[McpServerTool, Description(...)]`. Description explicitly says: "Pass the full skills directory path. For Claude Code project-local install — the recommended default — that's `<project_root>/.claude/skills`."
- [x] Ship the `skills/` directory with the build output via `<Content Include="..\skills\**\*" CopyToOutputDirectory="PreserveNewest" Link="skills\%(RecursiveDir)%(Filename)%(Extension)" />`. Verified `skills/toolproxy-serena-*/SKILL.md` lands in `bin/Debug/net10.0/skills/`.
- [x] End-to-end test: ran the tool against a temp dir from a small driver project. All four Serena skills installed; first-install response carries the warning; second install on the same dir reports `created_skills_root: false` and no warning; relative-path and empty-string inputs are rejected.

## Phase 5 — Tighten remaining MCP surface

Decide what stays exposed.

- [x] Remove `RefreshToolIndexAsync` (`refresh_tool_index`) — gone with `EnhancedLocalTool` in Phase 2.
- [x] Remove `GetToolIndexInfoAsync` (`get_tool_index_info`) — gone with `EnhancedLocalTool` in Phase 2.
- [x] Decided to replace `list_all_servers_and_tools_json` with a leaner `list_servers` returning `[{ name, description, tool_count }]`. Description is included because it's free and gives the agent meaningful context when invoked for debugging; the per-tool detail belongs in the skill bodies.
- [x] Final tool inventory: `call_external_tool`, `install_skills`, `list_servers`. (Live `tools/list` confirmation deferred to Phase 7 manual smoke test.)

## Phase 6 — Config schema cleanup

- [x] Audited `Configuration/AppSettings.cs` — `SemanticKernel` field already removed in Phase 2; nothing else embedding-related remained.
- [x] Confirmed `Configuration/McpServerConfig.cs` still covers transport, command, args, env, url. No new per-skill config block — skills are convention-discovered from the filesystem. The `Tools` list field is left in place; it's a fallback used by `ManagedMcpServer` when upstream tool discovery fails (in practice all configured servers have an empty list and rely on discovery).
- [x] `appsettings.json` already trimmed in Phase 2; no `appsettings.Development.json` exists.
- [x] Cleaned up a stale error message in `ManagedMcpServer.CallToolAsync` that referenced "the example given in the tool search result" — that path is gone; the message now just lists available tools.
- [ ] README sample-config update is folded into Phase 8 ("Documentation + sync").

## Phase 7 — Manual smoke test against the curated fleet

No automated test backfill (out of scope per idea doc) — verify the happy path manually.

**In-session wire-level checks completed** (with an empty `McpServers` config, since the curated fleet uses Windows `cmd.exe` commands that won't run on this Linux host):

- [x] Built and ran the proxy from `bin/Debug/net10.0/`. `/health` returns `200 OK`.
- [x] Drove a streamable-HTTP MCP handshake (`initialize` → `notifications/initialized` → `tools/list`). Server announces `{"name":"ToolProxy","version":"1.0.0.0"}` with the new instructions text.
- [x] `tools/list` returns exactly the three planned tools — `call_external_tool`, `install_skills`, `list_servers`. The `call_external_tool` schema confirms the renamed envelope: `{server, tool, arguments}` (all required).
- [x] Called `install_skills` over MCP against `/tmp/tp_smoke_install_…`. All four Serena skill directories landed, response includes the first-install warning.
- [x] Called `list_servers` over MCP. Returns `{"servers":[]}` for the empty-config smoke; under a real config this lists the configured fleet with name/description/tool_count.

**User-driven checks remaining** (require Claude Code + the user's actual upstream fleet):

- [ ] Start the proxy with the live `appsettings.json` against the real curated upstream set.
- [ ] From a Claude Code session: `tools/list` shows only the three local tools.
- [ ] `install_skills` to a test project's `.claude/skills`. Restart the session if it's the first install.
- [ ] Confirm the skill descriptions appear in `/skills` (or equivalent listing) and that invoking one loads its body.
- [ ] Drive an end-to-end Serena workflow through the proxy (e.g., `find_symbol` then `replace_symbol_body`) to validate dispatch correctness with the renamed params.

## Phase 8 — Documentation + sync

- [x] Rewrote top-level `README.md` and `ToolProxyMCP/README.md` to describe the skills-based design, the three-tool surface (`call_external_tool`, `install_skills`, `list_servers`), and the trimmed dependency stack. Stale semantic-search / Ollama / Semantic-Kernel content removed.
- [x] Decisions surfaced during implementation that are worth pinning back to the idea doc:
  - Dispatcher rename: `IToolIndexService` → `IMcpDispatcher` / `McpDispatcher`, with a single `CallExternalToolAsync(server, tool, arguments, ct)` method (the broader index-shaped surface is gone).
  - `install_skills` response shape: `{ skills_root, created_skills_root: bool, warning: string?, results: [{ name, path, status, error? }] }`. The first-install warning rides on the `warning` field rather than being inlined in `results`.
  - `list_all_servers_and_tools_json` was replaced with the leaner `list_servers` returning `[{ name, description, tool_count }]`. (Description is included because it's a free hint to the agent for "what's configured" debugging.)
  - The `Tools` array on `McpServerConfig` is retained as a fallback for upstream tool discovery failures, but is empty in normal use.
- [x] Plan retained as the change log for the executed sequence; not archived.

---

## Deferred to implementation / testing

Items the idea doc flags as "iron out in testing and usage":

- **`install_skills` response shape details** beyond the basics (per-skill `unchanged` vs `installed` distinction, file-mtime comparison, etc.) — start with the simple shape in Phase 4; add nuance only if the agent UX needs it.
- **`list_servers` vs `list_all_servers_and_tools_json`** — pick during Phase 5 based on what's actually useful at the prompt.
- **Dispatcher interface naming** — `IMcpDispatcher` is the proposed rename; defer the final call to the PR diff.
- **Skill body shipping format** — current plan copies the directory tree as-is. If a future skill needs sibling assets (images, JSON, etc.), the copy logic already handles that; no design change required.

## Out of scope (reaffirmed from idea doc)

- Persistent vector store / external DBs.
- Authentication / multi-tenant concerns.
- Hot-reload of upstream server config.
- Liveness probes for upstream servers.
- Test coverage backfill.
- ToolProxy.Chat changes.
