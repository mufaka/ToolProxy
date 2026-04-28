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
2. **A single MCP server may be represented by one *or more* skills.** The unit of skill generation is a skill spec, not a server — one server can map to N skill specs, and the proxy will materialize a separate `SKILL.md` for each. Small servers (3-5 tools) typically get a single skill. Large/multi-purpose servers (Serena-class, ~20 tools spanning explore / edit / memory / session) split into several focused skills so each one stays tight and the agent can pull in just the slice it needs. The N-skills-per-server mapping is declared explicitly in the proxy config.
3. For each skill spec, the proxy materializes a hand-authored `SKILL.md`:
   - **Frontmatter:** `name`, `description`, optional `when_to_use`. `description` + `when_to_use` share a 1,536-char cap (silent truncation past that).
   - **Body:** hand-authored prose covering operating principles, tool reference (with full `call_external_tool` JSON envelope repeated per example for robustness), and worked workflows. Body has no practical length cap — heavy guidance (Serena's philosophy, do/don't tables, etc.) lives here and is lazy-loaded.
4. **Skills are bootstrapped, then hand-edited.** **Decided.** For each declared skill spec, the proxy can generate a starter `SKILL.md` scaffold from the upstream tool catalog: stub frontmatter, an auto-populated tool reference section (one entry per tool in the spec, with names, parameter signatures, and a templated `call_external_tool` example), and placeholder sections for operating principles and workflows. The human then edits the scaffold to add the real prose — principles, sharpened descriptions, worked workflows, project-specific examples. The proxy is responsible for *creating* and *installing* skills; the human owns the prose.

   **Bootstrapping is an explicit CLI command, not an automatic startup behavior.** Generation runs only when the user invokes it (e.g. `toolproxy bootstrap`), so a running proxy never silently rewrites skill files. When bootstrap encounters an existing skill on disk, it warns and prompts per-skill for one of:
   - **Overwrite** — replace the existing file with the new scaffold (destructive).
   - **Ignore** — leave the existing file alone, skip generation for this skill.
   - **Copy alongside** — write the new scaffold next to the existing file under a sidecar name (e.g. `SKILL.bootstrap.md`) so the user can diff and merge externally.

   Sensible non-interactive flags should exist for scripting (e.g. `--on-conflict=overwrite|ignore|alongside`) but the interactive default protects hand-edited content.
5. The proxy's MCP surface stays minimal: a single `call_external_tool(server, tool, params)` dispatcher, plus maybe `list_servers` for debugging.
6. The agent sees only skill descriptions in its base context. When a request matches one (or more), the host loads those bodies, and the agent now has focused server-specific guidance plus exact invocation examples.

**Why this works well:**

- **Native progressive disclosure.** Skill bodies are loaded only when the description matches the task — exactly the narrowing we wanted, implemented by the host.
- **Heavy descriptions are free.** Serena's verbose guidance lives in the skill body; never loaded until needed. Subsumes the description-deferral idea entirely.
- **No protocol assumption.** Doesn't depend on `tools/list_changed`, doesn't depend on the client re-fetching anything mid-session.
- **Cross-client.** Skills follow the open Agent Skills standard. Should work on Claude Code, GitHub Copilot, and Codex; degrades gracefully on clients that don't (they get the static `call_external_tool` + `list_servers` surface and can ignore skills).
- **Live reload.** Claude Code watches skill directories during a session — proxy can regenerate after config changes and the running agent picks them up without restart. *(Caveat: the top-level skills directory has to exist at session start to be watched; creating it fresh requires a restart.)*
- **No LLM at generation time.** Skills are hand-authored markdown — no embedding, no phrase rewriting, no Ollama dependency. Sidesteps the brittleness that killed the previous design.

**Open questions:**
- [ ] **Skill output location.** Three plausible choices, each with tradeoffs:
  - `~/.claude/skills/toolproxy-<server>/SKILL.md` (personal scope) — always available, but visible in every Claude Code session even when proxy isn't running.
  - `<project>/.claude/skills/toolproxy-<server>/SKILL.md` (project scope) — only applies in projects that opt in; requires the proxy to know the project path.
  - A dedicated location plus `--add-dir` or symlink — more flexible, more setup.
  - Probably personal scope is simplest; revisit if cross-project pollution becomes annoying.
- [ ] **Skill naming / namespacing.** Prefix with `toolproxy-` to avoid colliding with the user's own skills. Hyphens, lowercase, ≤64 chars per the spec.
- [ ] **Refresh trigger.** Regenerate on proxy startup for sure. Also on a config-file watch? On a manual `refresh_skills` MCP tool the user/agent can call? Stale skills hang around if a server is removed from config — proxy should clean up its own old files.
- [ ] **Description content under the 1,536-char cap.** What goes in `description` vs `when_to_use` vs the body? Probably: description = "what this server does," when_to_use = "trigger phrases / when the agent should pick it," body = everything else.
- [ ] **Skill grouping for multi-purpose servers.** How is the per-server-to-multiple-skills mapping declared in config? Per-skill `name`, `description`, `tools` (subset of upstream tool names), and `path-to-source-markdown`?
- [ ] **Source vs. install location.** The human edits skill source files somewhere (e.g., `<proxy-config-dir>/skills/`); the proxy installs them to the agent's skill directory (e.g., `~/.claude/skills/`). Confirm that two-location model is what we want, vs. editing directly in the agent's directory and skipping the install step. Affects whether install is its own CLI subcommand (`toolproxy install-skills`) or implicit on startup.
- [ ] **Sidecar naming on conflict.** When bootstrap chooses "copy alongside," what's the filename pattern? `SKILL.bootstrap.md`? `SKILL.md.new`? Timestamp-suffixed? Predictable + diff-friendly is the goal; the user merges externally.
- [ ] **Drift detection on existing skills.** Optional: a separate CLI command (`toolproxy check-skills` or similar) that compares each existing skill's tool reference against the upstream catalog and reports what's stale, without touching files. Useful when upstream servers change between bootstrap runs. Not required for v1.
- [ ] **Static vs dynamic content.** Some upstream tools (e.g. Serena's `initial_instructions`) return session-specific state (active project, memories list). Static guidance lives in the skill; the agent calls the dynamic tool only when it needs runtime state. Possible future refinement: proxy intercepts the dynamic tool to strip the static portion (since the skill already delivered it).
- [ ] **Tool name collisions in the dispatcher.** Two upstream servers with `read_file`. Since we're calling through `call_external_tool(server, tool, params)`, the server name disambiguates — no namespacing prefix needed at the dispatcher layer. (Different from the previous design where this was a real concern.)

### B. Description deferral — *subsumed by A*

The original section B proposed a `descriptionMode` config (`full` / `summary` / `deferred`) to manage Serena-style context bloat at the MCP layer. Skills make this unnecessary: the skill *description* is the "summary" view (always loaded, kept short by the 1,536-char cap), and the skill *body* is the "deferred" view (loaded only when the agent picks the skill). Same outcome, no extra config surface, handled natively by the host.

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
- [ ] **Remove** `OllamaSharp` — skill generation is mechanical (templated from config + auto-fetched tool catalog), no LLM needed at generation time.
- [ ] **Remove** `Microsoft.Extensions.AI` if nothing else in the proxy uses it after the embedding code is gone.

## Decisions to make before coding

1. **Skill output location.** Personal (`~/.claude/skills/`) vs. project-local vs. dedicated dir + opt-in. Personal is simplest; pick unless we hit a concrete reason not to.
2. **Skill content split.** **Decided in part:**
   - Skills are hand-authored markdown (efficiency isn't always free).
   - JSON envelope for `call_external_tool` is repeated per example, not abbreviated (robustness against partial body loads).
   - Multi-purpose servers (Serena-class) split into multiple focused skills, one per cluster of related tools. Small servers stay as one skill.
   - **Still TBD:** the exact `description` vs `when_to_use` vs body breakdown — drafting one full example (Serena) will pin this down.
3. **Refresh model.** Regenerate-on-startup is given. Also: config-file watch? Manual `refresh_skills` MCP tool? Stale-file cleanup when servers are removed from config?
4. **Generation determinism.** Do we want skills to regenerate identically across runs (so the user can `git`-track them if they want), or is "best effort" fine? Affects whether timestamps/order matter in the output.
5. **Config schema.** Concrete shape for per-server: `name`, `description` (skill description), `whenToUse` (optional), `intro` (skill body intro), connection details. Worth sketching before implementing.
6. **Top-level skills directory existence.** Document the "must exist at session start" caveat — if we write to `~/.claude/skills/` and that dir doesn't exist, the user has to create it once before the proxy is useful. Probably the proxy should create it on startup (idempotent) and surface a one-time message asking the user to restart Claude Code if it had to create it fresh.

## Notes / parking lot

- An earlier draft proposed a small-LLM router living inside the proxy (qwen2.5:3b via Ollama). Superseded — the host agent is a better router than anything we'd run locally, and a passive proxy is simpler.
- A later draft proposed dynamic tool exposure via `notifications/tools/list_changed`. Superseded — client support is inconsistent (agents commonly cache tool lists per session), and the skills approach achieves the same progressive-disclosure outcome without a protocol-support assumption.
- Hybrid BM25 + embedding was discussed in the earliest draft. No longer relevant once semantic search is removed.

## Reference: Claude Code skill loading mechanics

(For implementation reference, sourced from [Extend Claude with skills](https://code.claude.com/docs/en/skills.md).)

- **Discovery locations, in precedence order:** enterprise (managed) → `~/.claude/skills/` (personal) → `<project>/.claude/skills/` (project) → plugin-bundled. Nested `.claude/skills/` directories are also auto-discovered in monorepos. Plugin skills use `plugin-name:skill-name` namespacing.
- **Live reload:** Claude Code watches skill directories during a session; adding/editing/removing a skill takes effect without restart. **Caveat:** the top-level skills directory must exist at session start to be watched — creating it fresh requires a session restart.
- **Frontmatter:** `name` optional (defaults to dir name; lowercase, numbers, hyphens, ≤64 chars). `description` recommended. `description` + `when_to_use` share a 1,536-char cap, silently truncated past that.
- **Body loading:** descriptions are always in context; bodies are lazy-loaded only when the agent (or user via `/skill-name`) invokes the skill. After invocation, the body stays in context for the rest of the session. After auto-compaction, recently invoked skills are re-attached up to a 25,000-token combined budget.
- **Cross-client:** skills follow the open Agent Skills standard ([agentskills.io](https://agentskills.io)). Other clients (GitHub Copilot, likely Codex) implement the base concept; Claude Code adds extensions (`disable-model-invocation`, `context: fork`, etc.) that aren't portable.
