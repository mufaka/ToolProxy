# ToolProxy Refactor — Implementation Plan

Trackable companion to `ToolProxyRefactorIdea.md`. The idea doc is the source of truth for *why*; this doc is the *how* and *what's done*. Phases are ordered to minimize churn — each phase should leave the build green.

Mark items `[x]` as completed. Add notes inline under a task when implementation reveals something the idea doc didn't cover (then sync to the idea doc if it's a real decision).

---

## Phase 1 — .NET 10 + dependency cleanup (prerequisite)

Isolate dependency churn from logic changes. Build must be green before Phase 2.

- [ ] Bump TFM `net9.0` → `net10.0` in `ToolProxyMCP/ToolProxy.csproj` (and any other projects participating in the refactor).
- [ ] Update SDK in `global.json` if pinned.
- [ ] Update `ModelContextProtocol` SDK from `0.3.0-preview.4` to current; resolve breaking changes.
- [ ] Remove NuGet refs: `Microsoft.SemanticKernel`, `Microsoft.SemanticKernel.Connectors.InMemory`, `OllamaSharp`.
- [ ] Remove `Microsoft.Extensions.AI` if no remaining usage after Phase 2.
- [ ] `dotnet build` clean across the solution. ToolProxy.Chat may break — acceptable per scope.
- [ ] Smoke test: launch the proxy with one upstream server and verify `tools/list` still works (legacy semantic tool is still wired up at this point — that's fine).

## Phase 2 — Strip semantic search

Delete the embedding/index path entirely. Leaves dispatch + upstream-server plumbing intact.

- [ ] Delete `ToolProxyMCP/Tools/EnhancedLocalTool.cs` (the `search_tools_semantic` tool and friends).
- [ ] Delete `ToolProxyMCP/Services/SemanticKernelToolIndexService.cs`.
- [ ] Delete `ToolProxyMCP/Services/EnhancedSemanticKernelToolIndexService.cs`.
- [ ] Delete `ToolProxyMCP/Models/ToolVectorRecord.cs`.
- [ ] Delete `ToolProxyMCP/Configuration/SemanticKernelSettings.cs`.
- [ ] Strip embedding/phrase-rewriting blocks from `appsettings.json` (and `appsettings.Development.json` if present).
- [ ] In `Program.cs`, remove DI registrations for the deleted services and any `Microsoft.Extensions.AI` / Ollama wiring.
- [ ] **Reshape the dispatcher service.** `IToolIndexService` is now misnamed — it's just a dispatcher. Choose one:
  - Rename interface + implementation to `IMcpDispatcher` / `McpDispatcher`, drop the index-shaped methods, keep only `CallExternalToolAsync` (and any helpers needed for `list_servers`).
  - Or keep the name temporarily and just trim methods. (Idea doc doesn't mandate either; pick the cleaner one in PR.)
- [ ] Re-home the surviving local tools (the dispatcher tool + introspection) into a new `Tools/LocalTool.cs` (or keep the name pending Phase 3).
- [ ] `dotnet build` clean. Run the proxy; confirm it starts and exposes only the surviving tools.

## Phase 3 — Rename dispatch parameters

Pin the `server` / `tool` / `arguments` shape that the skill drafts already use.

- [ ] In the dispatcher tool method, rename: `serverName` → `server`, `toolName` → `tool`, `parameters` → `arguments`. Update `[Description]` attributes accordingly.
- [ ] Cascade renames through the dispatcher interface and implementation (`CallExternalToolAsync` parameter names).
- [ ] Update the tool's top-level `[Description]` to reflect the new envelope (no references to "search results sample" — that path is gone).
- [ ] Confirm via `tools/list` against a live client that the parameter schema reads `server`, `tool`, `arguments`.
- [ ] Make one end-to-end dispatch call against an upstream server using the new shape (e.g., a Serena `check_onboarding_performed`).

## Phase 4 — Implement `install_skills` MCP tool

The agent-invoked installer.

- [ ] Add `Tools/SkillsInstallTool.cs` (or co-locate with the dispatcher tool — judgment call).
- [ ] Resolve the proxy's master skills directory: `Path.Combine(AppContext.BaseDirectory, "skills")`. Verify this path resolves correctly when run via `dotnet run` and from a published binary.
- [ ] Implement `install_skills(skills_root)`:
  - [ ] Validate `skills_root` is a non-empty absolute path; reject relative paths with a clear error.
  - [ ] Detect whether `skills_root` already exists (drives the first-install warning).
  - [ ] Create `skills_root` if missing.
  - [ ] For each subdirectory under `<proxy-install-dir>/skills/` containing a `SKILL.md`, copy the entire skill subtree to `<skills_root>/<skill-name>/`.
    - Decision: skill *names* are taken from the source directory name. The frontmatter `name` field is not parsed — directory name is canonical.
    - Overwrite unconditionally. No backup. No diff.
  - [ ] Collect per-skill outcome: `{ name, path, status: "installed" | "error", error?: string }`.
  - [ ] Return JSON: `{ skills_root, created_skills_root: bool, results: [...] }`. Include the first-install warning text in the response when `created_skills_root` is true.
- [ ] Register the tool with `[McpServerTool, Description(...)]`. Description must explicitly say: "Pass the full skills directory path. For Claude Code project-local install, that's `<project_root>/.claude/skills`."
- [ ] Ship the `skills/` directory with the build output. Add to `.csproj`:
  ```xml
  <ItemGroup>
    <Content Include="..\skills\**" CopyToOutputDirectory="PreserveNewest" Link="skills\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
  ```
  Verify after `dotnet publish` that `skills/toolproxy-serena-*/SKILL.md` lands next to the executable.
- [ ] End-to-end test: from an agent session, call `install_skills` with a temp dir; confirm all four Serena skill files land at expected paths and the response carries the first-install warning.

## Phase 5 — Tighten remaining MCP surface

Decide what stays exposed.

- [ ] Remove `RefreshToolIndexAsync` (`refresh_tool_index`) — no index to refresh.
- [ ] Remove `GetToolIndexInfoAsync` (`get_tool_index_info`) — no index, no info.
- [ ] Decide on `list_all_servers_and_tools_json`: keep as-is for debugging, or replace with a leaner `list_servers` returning just `[{ name, tool_count }]`. Idea doc allows either; lean toward `list_servers` for less surface.
- [ ] Final tool inventory after this phase: `call_external_tool`, `install_skills`, `list_servers` (or the JSON variant). Confirm `tools/list` matches.

## Phase 6 — Config schema cleanup

- [ ] Audit `Configuration/AppSettings.cs` for fields tied to embedding/phrase generation; remove.
- [ ] Confirm `Configuration/McpServerConfig.cs` still covers transport, command, args, env. No new per-skill config block — skills are convention-discovered from the filesystem.
- [ ] Update `appsettings.json` and any sample configs in the README to reflect the trimmed schema.

## Phase 7 — Manual smoke test against the curated fleet

No automated test backfill (out of scope per idea doc) — verify the happy path manually.

- [ ] Start the proxy with the live `appsettings.json` against the real curated upstream set.
- [ ] From a Claude Code session: `tools/list` shows only the three local tools.
- [ ] `install_skills` to a test project's `.claude/skills`. Restart the session if it's the first install.
- [ ] Confirm the skill descriptions appear in `/skills` (or equivalent listing) and that invoking one loads its body.
- [ ] Drive an end-to-end Serena workflow through the proxy (e.g., `find_symbol` then `replace_symbol_body`) to validate dispatch correctness with the renamed params.

## Phase 8 — Documentation + sync

- [ ] Update `README.md` to describe the skills-based design and the new local tool surface; remove references to semantic search.
- [ ] Update `ToolProxyRefactorIdea.md` if Phase 1-7 surfaced decisions worth pinning (response shape details, dispatcher rename outcome, etc.).
- [ ] Mark this plan complete and archive — or keep as a record of the executed sequence.

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
