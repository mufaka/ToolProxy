# ToolProxy Refactor — Idea Document

Working doc for scoping the next iteration of ToolProxyMCP. Not a plan yet — a place to collaboratively converge on what's in/out before any code moves.

## Context & framing

- **Audience:** in-house / personal use, small curated fleet of MCP servers (~5-10).
- **Runtime constraint:** keep external dependencies minimal. No external vector DB, no cloud LLMs required for core flow. Ollama-local stays the default.
- **What we're keeping:** the proxy shape itself — one MCP endpoint in front of N upstream servers, transport handling (STDIO/HTTP/SSE), lifecycle management, dispatcher. That plumbing is the expensive part and it works.
- **What we're rethinking:** the *narrowing mechanism*. Embedding tool descriptions + LLM-rewritten intent phrases didn't pan out reliably enough to depend on.

## Pain points being addressed

1. **Per-tool context bloat.** Servers like Serena ship long usage guidance with every tool description. At `tools/list` time this dominates the agent's context budget even before any tool is called.
2. **Tool count growth.** Even with a curated set, exposing every tool from every server is wasteful when most requests touch one server.
3. **Retrieval brittleness in the old design.** Cosine similarity on tool descriptions missed obvious matches and fired on near-misses; the phrase-rewriting workaround helped some but not enough. **Decision:** semantic search is being removed entirely (see below) — capable host agents make better narrowing decisions than a small embedding-based retriever ever could.

## Proposed direction

### A. Agent Skills as the narrowing mechanism (primary)

Lean on the host's existing **Agent Skills** primitive instead of trying to mutate the MCP tool list at runtime. The proxy generates one skill file per upstream MCP server; the host's skill-loading machinery handles the progressive disclosure for free.

**Why skills, not dynamic tool exposure.** `tools/list_changed` is in the MCP spec but client support is inconsistent — agents commonly cache tool lists per session and don't honor mid-session changes. Skills sidestep the protocol question entirely: they're filesystem-based, the host already knows how to load them lazily, and the standard is open and cross-client (Claude Code, GitHub Copilot, likely Codex, per [agentskills.io](https://agentskills.io)).

**Mechanism.**

1. On startup, the proxy connects to each configured upstream server and fetches its tool list (existing code already does this).
2. **A single MCP server may be represented by one *or more* skills.** Small servers (3-5 tools) typically get a single skill. Large/multi-purpose servers (Serena-class, ~20 tools spanning explore / edit / memory / session) split into several focused skills so each one stays tight and the agent can pull in just the slice it needs. Skills follow a **naming convention** rather than separate config registration: `toolproxy-<server>` for a single skill covering a server, `toolproxy-<server>-<group>` when split (as the bundled Serena examples do: `toolproxy-serena-explore`, `toolproxy-serena-edit`, `toolproxy-serena-memory`, `toolproxy-serena-session`). The proxy discovers skills by listing its skills directory; no per-skill config registration is needed.
3. For each skill spec, the proxy materializes a hand-authored `SKILL.md`:
   - **Frontmatter:** `name` and `description`. We do not use `when_to_use` — it's a Claude-Code-only extension functionally equivalent to appending to `description`, and skipping it keeps skills cross-client portable. Aim for 500-800 chars in description; the cross-client hard cap is 1024. The description must lead with user-facing capability (not implementation), use imperative "use this skill when..." phrasing, and include explicit example trigger phrases.
   - **Body:** hand-authored prose covering operating principles, tool reference (with full `call_external_tool` JSON envelope repeated per example for robustness), and worked workflows. Body has no practical length cap — heavy guidance (Serena's philosophy, do/don't tables, etc.) lives here and is lazy-loaded. Proxy plumbing details (the `call_external_tool` envelope shape, etc.) belong here, not in the description.
4. **Skills are hand-authored; the proxy is the source of truth.** **Decided.** The user authors skill content (frontmatter + body) and stores the master copies at `<proxy-install-dir>/skills/<skill-name>/SKILL.md` — one subdirectory per skill, the directory name matching the skill's `name` frontmatter field. To change what a project sees, the user edits the master and re-installs — there is no per-project customization, no in-place hand-edit at the install location to preserve.

5. **Installation is an MCP tool the agent invokes, not a CLI subcommand.** **Decided.** The proxy exposes:

   **`install_skills(skills_root)`** — writes the proxy's full skill catalog into `<skills_root>/<skill-name>/SKILL.md`, creating directories as needed and **unconditionally overwriting** any existing files. Returns the list of installed paths plus a one-shot warning if `<skills_root>` had to be created (on Claude Code, the user must restart once before live reload starts watching a freshly created directory; subsequent installs are seamless).

   The agent supplies `skills_root` based on user intent ("install ToolProxy's skills here") and its host's conventions. For Claude Code project-local install — the recommended default — that's `<project_root>/.claude/skills`. The agent passes the full skills root, not just the project root; the proxy stays host-neutral and does not append `.claude/skills` itself. Skills should always be installed **project-locally, never globally** by user-facing guidance, but the proxy doesn't enforce this — it writes wherever the agent points. There is no conflict prompt, no copy-alongside, no `--mode` flag: the proxy is the source of truth and the install is destructive by design. If a user wants different content in different projects, they curate that at the proxy source level.
6. The proxy's MCP surface stays minimal: `call_external_tool(server, tool, arguments)` for dispatch, `install_skills(skills_root)` for installation, plus maybe `list_servers` for debugging. **Parameter naming note:** `server` / `tool` / `arguments` are the pinned names — they supersede the current code's `serverName` / `toolName` / `parameters` (`EnhancedLocalTool.CallExternalToolAsync`). Shorter names cut per-call token overhead (see "Token economics" below), and `arguments` matches the MCP `tools/call` wire spec (`params.arguments`). The four hand-authored skill drafts already use this shape; the implementation must follow rather than carrying over the old names.
7. The agent sees only skill descriptions in its base context. When a request matches one (or more), the host loads those bodies, and the agent now has focused server-specific guidance plus exact invocation examples.

**Why this works well:**

- **Native progressive disclosure.** Skill bodies are loaded only when the description matches the task — exactly the narrowing we wanted, implemented by the host.
- **Heavy descriptions are free.** Serena's verbose guidance lives in the skill body; never loaded until needed. Subsumes the description-deferral idea entirely.
- **No protocol assumption.** Doesn't depend on `tools/list_changed`, doesn't depend on the client re-fetching anything mid-session.
- **Cross-client.** Skills follow the open Agent Skills standard. Should work on Claude Code, GitHub Copilot, and Codex; degrades gracefully on clients that don't (they get the static `call_external_tool` + `list_servers` surface and can ignore skills).
- **Live reload.** Claude Code watches `<skills_root>` (typically `<project>/.claude/skills/` for project-local installs) during a session — once the directory exists, re-running `install_skills` updates content in place and the agent picks up changes without restart. *(One-time caveat: the **first** install creates `<skills_root>` if it didn't exist; if the session was started before then, the user has to restart Claude Code once for live-watch to engage. Subsequent installs are seamless.)*
- **No LLM at generation time.** Skills are hand-authored markdown — no embedding, no phrase rewriting, no Ollama dependency. Sidesteps the brittleness that killed the previous design.

**Token economics — proxy vs. native tool exposure.**

The wrapper adds a small per-call cost but pays for itself on base-context savings before the first tool fires.

*Per-call request-side overhead* (every dispatch through the proxy):
- Wrapper tool name `call_external_tool` (~5 tok) vs. an upstream tool name like `find_symbol` (~3 tok) → +2 tok.
- Envelope keys `"server"` / `"tool"` / `"arguments"` plus their string values → ~12-15 tok.
- **Net: ~15-20 tokens per tool call.** This is the case for keeping the dispatch parameter names short — `server` / `tool` / `arguments` rather than `serverName` / `toolName` / `parameters`.

*Base-context savings* (every turn, regardless of whether a tool fires):
- Native exposure of a Serena-class server: ~20 tools × 150-300 tok of description each = **3,000-6,000 tok permanently in the base context.**
- Skills replacement: 4 skill descriptions × ~200 tok ≈ **~800 tok.**
- **Net: ~2,200-5,200 tok/turn saved.**

*Per-skill body load* (one-time per session, only when invoked):
- Bodies range ~700-2,000 tok across the four current Serena drafts (session ~700, memory ~1,500, explore ~1,500, edit ~2,000).
- Lazy by design: only the matching skill's body is pulled in.

The per-turn base savings clear the per-call wrapper overhead inside a single turn, even if no tools fire — description bloat hits every turn, the wrapper hits only on dispatches. The wrapper cost is amortized; the base savings are constant.

**Resolved (recorded for reference):**
- ~~Static vs dynamic content (e.g., `initial_instructions`).~~ Resolved by exclusion. Skill authors simply omit upstream tools that bundle static guidance with dynamic state when the dynamic state has cleaner alternatives via other tools. No proxy-side intercept mechanism needed. This turns out to be a Serena-specific quirk — most MCP servers don't bundle their usage instructions into a callable tool. The example skills accordingly *exclude* Serena's `initial_instructions`: its memories list is already covered by `list_memories` in the memory skill, and "active project" status is inferable from whether other tools succeed or fail.
- ~~Skill output location at install time.~~ Agent-supplied `skills_root` — the agent passes the full target directory; the proxy doesn't append `.claude/skills`. Convention is `<project_root>/.claude/skills` for Claude Code project-local install (the recommended default). User-facing guidance is "always project-local"; the proxy is host-neutral and writes wherever the agent points.
- ~~Master skill source location.~~ `<proxy-install-dir>/skills/<skill-name>/SKILL.md`. Convention-based, no config path needed.
- ~~Skill grouping for multi-purpose servers.~~ Naming convention only — `toolproxy-<server>` for a single skill, `toolproxy-<server>-<group>` when split. No config registration; the proxy discovers skills by listing the skills directory.
- ~~Description content convention.~~ Use `description` only (no `when_to_use` — it's a Claude-Code-only extension, functionally appended to description, drops cross-client portability). Style rules pinned by the four Serena drafts:
  - **Lead with user-facing capability**, not implementation ("Locate classes..." not "Semantic exploration via the Serena MCP server...").
  - **Imperative phrasing.** "Use this skill when..." not "This skill does..."
  - **Include explicit trigger phrases** in quotes, especially casual ones the user might naturally say without naming any tool ("where is the login handler", "fix this method").
  - **Be pushy on capability skills** ("Strongly prefer over the host's X tool"). Be the *opposite* on fallback/setup skills (the session skill says "never preemptively") so they don't false-trigger.
  - **Keep proxy plumbing out of the description.** "Routes through `call_external_tool`" belongs in the body, not in the trigger surface.
  - **Aim for 500-800 chars** in description; the agentskills.io 1024 cap is the hard ceiling. Claude Code's combined cap is 1536, but staying under 1024 keeps cross-client safe.
- ~~Refresh trigger.~~ No automatic refresh. The agent calls `install_skills` when the user asks; the proxy never silently writes skill files.
- ~~Source vs. install location.~~ Proxy source dir is the source of truth; install destination is `<project_root>/.claude/skills/`. Two locations, but the proxy owns the relationship.
- ~~Sidecar naming on conflict.~~ Not needed — install is unconditional overwrite.
- ~~Drift detection.~~ Not needed — the user re-installs to refresh; the proxy's source files are always current.
- ~~Tool name collisions in the dispatcher.~~ The `server` parameter on `call_external_tool` disambiguates.
- ~~Skill naming / namespacing.~~ Prefix `toolproxy-`; hyphens, lowercase, ≤64 chars. Naming convention `toolproxy-<server>[-<group>]`.

### B. Description deferral — *subsumed by A*

The original section B proposed a `descriptionMode` config (`full` / `summary` / `deferred`) to manage Serena-style context bloat at the MCP layer. Skills make this unnecessary: the skill *description* is the "summary" view (always loaded, kept short — we target 500-800 chars under the 1024-char cross-client cap), and the skill *body* is the "deferred" view (loaded only when the agent picks the skill). Same outcome, no extra config surface, handled natively by the host.

The remaining design choice — what goes in description vs. body — is now an open question under (A) rather than its own mechanism.

### C. Remove semantic search entirely

**Decided.** The embedding/vector-store path goes away — not demoted, not kept as a fallback. The agent-driven discovery in (A) supersedes it: the host agent's tool-selection is more reliable than cosine retrieval, and once tools are exposed natively, the agent doesn't need a `search_tools` shim at all.

What this removes from the codebase:
- `EnhancedLocalTool.cs` (the `search_tools_semantic` MCP tool).
- `SemanticKernelToolIndexService.cs` and `EnhancedSemanticKernelToolIndexService.cs`.
- `ToolVectorRecord` and the in-memory vector store wiring.
- The phrase-generation prompt config and Ollama embedding-model config.
- `Microsoft.SemanticKernel` and the in-memory connector packages (see dependency section).

What stays:
- `call_external_tool` dispatcher logic (still needed for the Variant 1 fallback path, if we keep one).
- The introspection tools (`list_all_servers_and_tools_json`, `get_tool_index_info` — though the latter loses meaning and probably also goes).
- All of `McpManager` / `ManagedMcpServer` / `McpHostedService` — the upstream-server plumbing.

## Out of scope (for this refactor)

- Persistent vector store / external DBs.
- Authentication / multi-tenant concerns.
- Hot-reload of server config.
- Liveness probes for upstream servers.
- Test coverage backfill (worth doing, but separate work).
- **`ToolProxy.Chat`.** Considered deprecated / unsupported as of this refactor. Stays in the repo and solution as historical reference and as a future home for first-class skill support, but receives no changes here. The proxy is not constrained to keep it working; if its dependencies break as a side effect of the dependency cleanup, that's acceptable.

## .NET 10 / dependency upgrade

Treat as a separate, prerequisite step:

- [ ] .NET 9 → .NET 10 SDK + TFM bump.
- [ ] `ModelContextProtocol` SDK update — currently `0.3.0-preview.4`; check current version and breaking changes.
- [ ] **Remove** `Microsoft.SemanticKernel` and `Microsoft.SemanticKernel.Connectors.InMemory` entirely (no longer needed once C is gone).
- [ ] **Remove** `OllamaSharp` — skills are hand-authored, no LLM needed.
- [ ] **Remove** `Microsoft.Extensions.AI` if nothing else in the proxy uses it after the embedding code is gone.

## Decisions to make before coding

1. **Config schema for upstream servers.** Mostly inherits from existing `appsettings.json`: per-server connection details (transport, command, args, env, etc.). No per-skill config registration is needed — skills are discovered by listing `<proxy-install-dir>/skills/`. Worth confirming the existing schema still fits once the embedding-related fields are stripped out.
2. **`install_skills` response shape.** Apart from the list of installed paths, what else does it return? The first-install warning when `<skills_root>` had to be created; possibly a per-skill outcome (installed, unchanged, error). Pin down before implementing the tool.

## Notes / parking lot

- An earlier draft proposed a small-LLM router living inside the proxy (qwen2.5:3b via Ollama). Superseded — the host agent is a better router than anything we'd run locally, and a passive proxy is simpler.
- A later draft proposed dynamic tool exposure via `notifications/tools/list_changed`. Superseded — client support is inconsistent (agents commonly cache tool lists per session), and the skills approach achieves the same progressive-disclosure outcome without a protocol-support assumption.
- An intermediate draft proposed a `toolproxy bootstrap` CLI subcommand with interactive overwrite/ignore/copy-alongside conflict prompts and an `install_skills` step that returned skill bytes for the agent to write. Superseded — the agent calling `install_skills(skills_root)` and letting the proxy write directly is simpler, keeps skill bodies out of conversation context, and removes the entire conflict-handling surface (proxy is source of truth, install is unconditional overwrite, project-local by guidance).
- Hybrid BM25 + embedding was discussed in the earliest draft. No longer relevant once semantic search is removed.

## Reference: Claude Code skill loading mechanics

(For implementation reference, sourced from [Extend Claude with skills](https://code.claude.com/docs/en/skills.md).)

- **Discovery locations, in precedence order:** enterprise (managed) → `~/.claude/skills/` (personal) → `<project>/.claude/skills/` (project) → plugin-bundled. Nested `.claude/skills/` directories are also auto-discovered in monorepos. Plugin skills use `plugin-name:skill-name` namespacing.
- **Live reload:** Claude Code watches skill directories during a session; adding/editing/removing a skill takes effect without restart. **Caveat:** the top-level skills directory must exist at session start to be watched — creating it fresh requires a session restart.
- **Frontmatter:** `name` optional (defaults to dir name; lowercase, numbers, hyphens, ≤64 chars). `description` recommended. `description` + `when_to_use` share a 1,536-char cap, silently truncated past that.
- **Body loading:** descriptions are always in context; bodies are lazy-loaded only when the agent (or user via `/skill-name`) invokes the skill. After invocation, the body stays in context for the rest of the session. After auto-compaction, recently invoked skills are re-attached up to a 25,000-token combined budget.
- **Cross-client:** skills follow the open Agent Skills standard ([agentskills.io](https://agentskills.io)). Other clients (GitHub Copilot, likely Codex) implement the base concept; Claude Code adds extensions (`disable-model-invocation`, `context: fork`, etc.) that aren't portable.
