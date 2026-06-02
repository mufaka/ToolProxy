---
name: toolproxy-gitnexus
description: Code-intelligence over a precomputed knowledge graph via GitNexus — find execution flows for a concept, get a 360-degree view of a symbol (callers, callees, the flows it sits in), compute a symbol's blast radius before editing, map your current git diff to the symbols and flows it touches, and rename a symbol across the call graph. Use any time the user asks how a feature works end-to-end ("how does X work", "show me the auth flow"), what a change would break ("what breaks if I change this", "is this safe to rename", "blast radius"), what pending edits affect (before a commit), or wants a graph-aware rename. Because ToolProxy fronts many repos, every call MUST pass the indexed `repo` name — found in the project's AGENTS.md. Prefer over grep for tracing execution flows and over guesswork for impact analysis.
---

# GitNexus: code intelligence (via ToolProxy)

GitNexus indexes a repo into a knowledge graph (symbols, call relationships, clustered functional areas, and named execution flows) and answers questions that span the graph: how a flow runs, what a symbol connects to, and what a change ripples into. These are graph queries against a precomputed index — not live source reads.

## Always pass `repo`

GitNexus serves **all indexed repos** from one process, and ToolProxy is a central gateway whose working directory is **not** the user's project — so the "omit `repo` when only one repo is indexed" default the tool schemas describe is unreliable here. **Pass `repo` on every call.**

Find the name in the **current project's `AGENTS.md`** (or `CLAUDE.md`): the GitNexus block contains a line like

> This project is indexed by GitNexus as **ToolProxy** (689 symbols, …).

The bolded token (`ToolProxy` here) is the `repo` value. If `AGENTS.md` has no GitNexus block, call `list_repos` to discover the registered names, then pass the right one. Every example below uses `"repo": "ToolProxy"` — substitute the name for the project you're actually working in.

## Operating principles for code intelligence

- **Impact before edit.** Before modifying a function, class, or method, run `impact` with `direction: "upstream"` to learn the blast radius (direct callers, affected flows, risk). Report HIGH/CRITICAL risk to the user before proceeding. This is the load-bearing convention of this project.
- **`detect_changes` before commit.** Map the working diff to the symbols and execution flows it touches, and confirm the change only affects what you expect.
- **`query`, not grep, for flows.** To understand how a concept works, `query` returns results grouped by execution flow and ranked by relevance — far better than text search for tracing behavior across files.
- **`rename`, never find-and-replace.** Renames go through `rename`, which understands the call graph and tags each edit with confidence. It previews by default (`dry_run: true`) — review before applying.
- **Disambiguate by `uid` when you have one.** Once a prior result hands you a symbol `uid`, pass it (`target_uid` / `symbol_uid` / `uid`) for a zero-ambiguity lookup instead of re-resolving by name.
- **The index can be stale.** Tools may return a staleness warning. The fix — `npx gitnexus analyze` — is a terminal command, not an MCP tool; surface it to the user and let them run it. Don't try to refresh the index through `call_external_tool`.
- **Resources are not reachable through ToolProxy.** GitNexus also exposes `gitnexus://repo/{name}/...` MCP *resources* (context, clusters, processes, schema). ToolProxy's `call_external_tool` dispatches *tool* calls only — those resource URIs are not available on this path. Use the `query` and `context` tools to get the same information.

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "gitnexus",
  "tool": "<tool name>",
  "arguments": { "repo": "<indexed repo name>", /* tool-specific parameters */ }
}
```

The tool names are **unprefixed** — `query`, `context`, `impact`, not `gitnexus_query`. Every example below repeats the full envelope and includes `repo` so it can be copied without inferring structure from elsewhere.

## Tools

### `query` — find execution flows for a concept

Searches the graph for execution flows (call chains) and symbols related to a concept, grouped by flow and ranked by relevance. The right first move for "how does X work" and for locating where behavior lives.

**Key parameters:**
- `query` — required; the concept in natural language or keywords (e.g. `"dispatch a tool call to an upstream server"`).
- `task_context` / `goal` — optional free text describing what you're working on and what you want to find; both improve ranking.
- `limit` (default 5) / `max_symbols` (default 10) — cap processes returned and symbols per process.
- `include_content` — set `true` to pull full symbol source inline instead of just locations.
- `repo` — **always pass it.**

**Example — trace how upstream tool dispatch works:**

```json
{
  "server": "gitnexus",
  "tool": "query",
  "arguments": {
    "repo": "ToolProxy",
    "query": "dispatch an external tool call to an upstream MCP server",
    "goal": "the method that forwards a tool call to the upstream client",
    "limit": 10
  }
}
```

### `context` — 360-degree view of a symbol

Returns a symbol's categorized incoming/outgoing references (calls, imports, extends, implements, methods, properties, overrides), the execution flows it participates in, and its file location. Use after `query` to deep-dive a specific symbol, or when the user asks "what calls this" / "what does this depend on".

**Key parameters:**
- `name` — the symbol name (e.g. `"CallToolAsync"`).
- `uid` — a symbol UID from a prior result; zero-ambiguity, skips name resolution.
- `file_path` / `kind` — disambiguate a common name (`kind` ∈ `Function`, `Class`, `Method`, `Interface`, `Constructor`).
- `include_content` — set `true` to include source for the symbol and its neighbors.
- `repo` — **always pass it.**

**Example — full context on a method:**

```json
{
  "server": "gitnexus",
  "tool": "context",
  "arguments": {
    "repo": "ToolProxy",
    "name": "CallToolAsync",
    "kind": "Method",
    "include_content": false
  }
}
```

### `impact` — blast radius of a change

Returns affected symbols grouped by depth, plus a risk assessment, affected execution flows, and affected modules. **Run this before editing any symbol.**

**Key parameters:**
- `target` — required; the symbol (function/class/file) name to analyze.
- `direction` — required; `"upstream"` = what depends on `target` / what breaks if you change it (use this before editing); `"downstream"` = what `target` depends on.
- `maxDepth` — relationship hops to trace (default 3, clamped 1–32). Lower it (e.g. 2) for a tighter, faster picture.
- `file_path` / `kind` — disambiguate a common name.
- `includeTests` (default false) / `minConfidence` (0–1) / `relationTypes` — narrow the trace.
- `target_uid` — exact symbol UID from a prior result; skips target resolution.
- `repo` — **always pass it.**

**Example — what breaks if I change a dispatcher method:**

```json
{
  "server": "gitnexus",
  "tool": "impact",
  "arguments": {
    "repo": "ToolProxy",
    "target": "CallToolAsync",
    "direction": "upstream",
    "maxDepth": 2
  }
}
```

### `detect_changes` — map the git diff to symbols and flows

Maps git diff hunks to indexed symbols, then traces which execution flows are impacted. Run before committing to confirm the change's scope.

**Key parameters:**
- `scope` — `"unstaged"` (default), `"staged"`, `"all"`, or `"compare"`.
- `base_ref` — branch/commit to diff against; **only used when `scope` is `"compare"`** (e.g. `"main"`).
- `repo` — **always pass it.**

**Example — check uncommitted working changes:**

```json
{
  "server": "gitnexus",
  "tool": "detect_changes",
  "arguments": {
    "repo": "ToolProxy",
    "scope": "unstaged"
  }
}
```

**Example — compare the branch against `main`:**

```json
{
  "server": "gitnexus",
  "tool": "detect_changes",
  "arguments": {
    "repo": "ToolProxy",
    "scope": "compare",
    "base_ref": "main"
  }
}
```

### `rename` — call-graph-aware rename

Finds every reference via the graph (high confidence) plus regex text search (lower confidence) and returns confidence-tagged edits across all affected files. Previews by default. Use this instead of find-and-replace.

**Key parameters:**
- `new_name` — required; the new symbol name.
- `symbol_name` — the current name (or `symbol_uid` for an exact match).
- `file_path` — disambiguate when the name is not unique.
- `dry_run` — default `true` (preview only). Set `false` to apply the edits.
- `repo` — **always pass it.**

**Example — preview a rename:**

```json
{
  "server": "gitnexus",
  "tool": "rename",
  "arguments": {
    "repo": "ToolProxy",
    "symbol_name": "CallToolAsync",
    "new_name": "InvokeToolAsync",
    "dry_run": true
  }
}
```

### `cypher` — raw graph query

Runs a Cypher query directly against the knowledge graph. Reach for it when the prepackaged tools don't shape the answer you need (custom traversals, aggregate counts, unusual relationship paths).

**Graph schema** (the schema resource isn't reachable through ToolProxy, so it's inlined here):
- **Nodes:** `File`, `Folder`, `Function`, `Class`, `Interface`, `Method`, `Community`, `Process`
- **Edges:** a single `CodeRelation` relationship carrying a `type` property — `CALLS`, `IMPORTS`, `EXTENDS`, `IMPLEMENTS`, `DEFINES`, `MEMBER_OF`, `STEP_IN_PROCESS`

**Key parameters:**
- `query` — required; the Cypher text.
- `repo` — **always pass it.**

**Example — who calls a given function:**

```json
{
  "server": "gitnexus",
  "tool": "cypher",
  "arguments": {
    "repo": "ToolProxy",
    "query": "MATCH (caller)-[:CodeRelation {type: 'CALLS'}]->(f:Function {name: 'CallToolAsync'}) RETURN caller.name, caller.filePath"
  }
}
```

### `list_repos` — discover indexed repos

Lists every repo registered in the GitNexus index, with name, path, indexed date, and stats. Use it to find the exact `repo` name when `AGENTS.md` doesn't have a GitNexus block or when several repos are indexed. Takes no arguments (this is the one call where `repo` doesn't apply).

```json
{
  "server": "gitnexus",
  "tool": "list_repos",
  "arguments": {}
}
```

### Other tools on this server

The server also exposes route/API-surface analysis (`route_map`, `tool_map`, `shape_check`, `api_impact` — most useful in web-API repos) and cross-repo group management (`group_list`, `group_sync`). They take the same `repo` argument; dispatch them with the same envelope and inspect the schema the host returns on first call rather than guessing.

## Common workflows

### Understand how a feature works

1. `query` with the concept (and `repo`) → execution flows and the symbols in each.
2. `context` on the key symbol from the top flow → callers, callees, flows it sits in.
3. Read the implementation for the specific symbols that matter.

### Safe edit (the project default)

1. `impact` with `direction: "upstream"`, `maxDepth: 2` on the symbol you intend to change.
2. If risk is HIGH/CRITICAL, report the blast radius to the user before editing.
3. Make the edit.
4. `detect_changes` (`scope: "unstaged"`) before committing to confirm only the expected symbols and flows are affected.

### Rename across the call graph

1. `rename` with `dry_run: true` → review the confidence-tagged edit set.
2. If the edits look right, re-run with `dry_run: false` to apply.
3. `detect_changes` to confirm scope before committing.
