---
name: toolproxy-srclight-workspace
description: Get oriented in an Srclight-indexed workspace — list indexed projects, inspect codebase maps and index status, trigger reindexing, check embedding coverage, inspect build targets, and find platform-conditional code. Use this when the user asks what repositories are indexed, whether the index is healthy, what the build produces, or where platform-specific code lives. Reach for this before symbol-level search when the first question is about workspace shape rather than a specific symbol.
---

# Srclight: workspace and index state (via ToolProxy)

Workspace-level visibility for Srclight: what projects are indexed, how healthy the index is, what the build targets are, and where platform-specific code is concentrated.

## Operating principles for workspace inspection

- **Start with the map on unfamiliar codebases.** `srclight_codebase_map` is the fastest high-level orientation pass.
- **Use `project` whenever workspace mode makes results ambiguous.** Several Srclight tools require or strongly benefit from an explicit project name when multiple repos are indexed.
- **Don't reindex casually.** `srclight_reindex` is for stale or missing results, not a routine first step.
- **Check embedding coverage before leaning on semantic search.** `srclight_embedding_status` tells you whether semantic and hybrid search have the data they need.
- **Build targets and platform conditionals are structural tools.** Use them to understand how the repo is assembled before guessing where a feature lives.

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "srclight",
  "tool": "<tool name>",
  "arguments": { /* tool-specific parameters */ }
}
```

Every example below repeats the full envelope so it can be copied without inferring structure from elsewhere.

## Tools

### `srclight_list_projects` — list indexed workspace projects

Use this first when Srclight is running in workspace mode and you need the exact project names for later calls.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_list_projects",
  "arguments": {}
}
```

### `srclight_codebase_map` — get a high-level codebase overview

Returns project stats, language breakdown, symbol counts, directory structure, and hotspots. The right first move for "what is in this repo?"

**Key parameter:**
- `project` — optional project filter in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_codebase_map",
  "arguments": {
    "project": "toolproxy"
  }
}
```

### `srclight_index_status` — inspect index state

Use this when search behavior seems stale, incomplete, or inconsistent.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_index_status",
  "arguments": {}
}
```

### `srclight_reindex` — refresh the index

Incrementally updates the index for the whole repo or a specific path. Reach for it only when you have evidence the index is outdated.

**Key parameter:**
- `path` — optional path to reindex instead of the whole repo.

**Example — reindex one subtree:**

```json
{
  "server": "srclight",
  "tool": "srclight_reindex",
  "arguments": {
    "path": "ToolProxyMCP/Services"
  }
}
```

### `srclight_embedding_status` — check semantic-search coverage

Shows whether embeddings exist and how much of the indexed symbol graph is covered.

**Key parameter:**
- `project` — optional project filter in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_embedding_status",
  "arguments": {
    "project": "toolproxy"
  }
}
```

### `srclight_get_build_targets` — inspect build outputs and dependencies

Parses build metadata such as CMake, csproj, package.json, or Cargo manifests to show targets, sources, dependencies, and platform conditions.

**Key parameter:**
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_build_targets",
  "arguments": {
    "project": "toolproxy"
  }
}
```

### `srclight_platform_conditionals` — list platform-guarded code blocks

Use this to find `#ifdef` / `#if defined()` regions or otherwise answer "where is the platform-specific code?"

**Key parameters:**
- `project` — required in workspace mode.
- `platform` — optional filter such as `windows`, `linux`, or `android`.

**Example — only Windows-specific guards:**

```json
{
  "server": "srclight",
  "tool": "srclight_platform_conditionals",
  "arguments": {
    "project": "toolproxy",
    "platform": "windows"
  }
}
```

## Common workflows

### Getting oriented in a new indexed workspace

1. `srclight_list_projects` if project names are unknown.
2. `srclight_codebase_map` for the high-level structure.
3. `srclight_get_build_targets` if you need to understand how it builds.

### Diagnosing stale search results

1. `srclight_index_status` to inspect the current index state.
2. `srclight_embedding_status` if the issue is semantic or hybrid search quality.
3. `srclight_reindex` only if the index is actually stale.

### Understanding platform split

1. `srclight_platform_conditionals` to find guarded code regions.
2. `srclight_get_build_targets` to see which targets and sources vary by platform.

## See also

- **`toolproxy-srclight-explore`** — symbol discovery and file-level exploration.
- **`toolproxy-srclight-impact`** — callers, callees, hierarchies, tests, and blast radius.
- **`toolproxy-srclight-history`** — recent changes, hotspots, blame, and work in progress.