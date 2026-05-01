---
name: toolproxy-srclight-history
description: Inspect how code changed over time with Srclight — browse recent commits, see what changed for a symbol's file, identify churn hotspots, inspect uncommitted work, and blame a symbol to understand who changed it and when. Use this when the user asks what changed recently, who last touched a symbol, where the riskiest files are, or what is currently modified in the repo.
---

# Srclight: history and change analysis (via ToolProxy)

Git-aware context for understanding recency, ownership, churn, and current uncommitted work.

## Operating principles for history analysis

- **Choose the narrowest history view that answers the question.** `srclight_recent_changes` is repo-wide; `srclight_changes_to` and `srclight_blame_symbol` are symbol-centered.
- **Hotspots are risk signals, not guilt.** `srclight_git_hotspots` identifies change-prone files; it does not prove a file is bad, only that it deserves caution.
- **Use `srclight_whats_changed` before assuming the index or git history is the whole story.** Uncommitted edits may explain why the current workspace differs from HEAD.
- **Blame is best for provenance, not design review.** `srclight_blame_symbol` tells you who changed a symbol and when; it does not explain why unless the commit messages do.
- **Path and author filters are leverage.** When the user already knows the area or engineer, narrow `srclight_recent_changes` instead of pulling the repo's entire recent history.

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

### `srclight_recent_changes` — list recent commits with changed files

Use this for "what changed recently?" at repo or subtree scope.

**Key parameters:**
- `n` — optional number of commits.
- `author` — optional author-name filter.
- `path_filter` — optional path prefix filter.
- `project` — optional project filter in workspace mode.

**Example — recent changes under services:**

```json
{
  "server": "srclight",
  "tool": "srclight_recent_changes",
  "arguments": {
    "project": "toolproxy",
    "n": 10,
    "path_filter": "ToolProxyMCP/Services/"
  }
}
```

### `srclight_changes_to` — show change history for a symbol's file

Best when the user cares about one symbol and wants the history of the file containing it.

**Key parameters:**
- `symbol_name` — symbol to track.
- `n` — optional number of commits.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_changes_to",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync",
    "n": 8
  }
}
```

### `srclight_git_hotspots` — find high-churn files

Use this before risky edits or when the user asks where the fragile areas are.

**Key parameters:**
- `n` — optional number of files.
- `since` — optional time window like `30.days` or `1.year`.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_git_hotspots",
  "arguments": {
    "project": "toolproxy",
    "n": 15,
    "since": "6.months"
  }
}
```

### `srclight_whats_changed` — show uncommitted work in progress

Returns staged, unstaged, and untracked files. Use this before blaming the index or git history for discrepancies.

**Key parameter:**
- `project` — optional project filter in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_whats_changed",
  "arguments": {
    "project": "toolproxy"
  }
}
```

### `srclight_blame_symbol` — show who last changed a symbol

Returns last modifier, age, unique commit/author counts, and the list of commits touching the symbol's line range.

**Key parameters:**
- `symbol_name` — target symbol.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_blame_symbol",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync"
  }
}
```

## Common workflows

### Understanding recent repo activity

1. `srclight_recent_changes` for the broad timeline.
2. `srclight_whats_changed` to include local, uncommitted work.

### Assessing risk before editing

1. `srclight_git_hotspots` to find churn-heavy files.
2. `srclight_changes_to` or `srclight_blame_symbol` on the exact symbol you plan to touch.

### Figuring out who changed something and when

1. `srclight_blame_symbol` for the symbol-level answer.
2. `srclight_changes_to` if you need the surrounding file history.

## See also

- **`toolproxy-srclight-explore`** — inspect the code itself once history points you to the right place.
- **`toolproxy-srclight-impact`** — understand what else depends on the code you now know is hot or recently changed.
- **`toolproxy-srclight-workspace`** — workspace map, build targets, and platform-level structure.