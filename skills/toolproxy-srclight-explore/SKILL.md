---
name: toolproxy-srclight-explore
description: Search and inspect code symbols with Srclight — search by keyword or meaning, list the symbols in a file, fetch full symbol details, or grab lightweight signatures before reading bodies. Use this when the user asks where something is defined, what symbols exist in a file, or wants to search by concept rather than exact spelling. Prefer this over reading whole files when the goal is code discovery, especially in large indexed repositories.
---

# Srclight: symbol discovery (via ToolProxy)

Symbol-aware search and retrieval for indexed codebases. These tools answer "where is this thing, what symbols live here, and what does this API look like?"

## Operating principles for symbol discovery

- **Use the lightest read that answers the question.** `srclight_get_signature` is cheaper than `srclight_get_symbol`; `srclight_symbols_in_file` is cheaper than reading the file.
- **`srclight_hybrid_search` is the default search when you are unsure.** It combines exact text matching with semantic similarity and falls back gracefully when embeddings are missing.
- **Use pure semantic search only when wording is fuzzy.** If you know the symbol name or likely keywords, `srclight_search_symbols` or `srclight_hybrid_search` is usually better.
- **Filter by `kind` when the user has already constrained the answer.** "Find the class" and "find the method" should not search the same surface.
- **Pass `project` in workspace mode.** It sharpens results and avoids mixing similarly named symbols across repositories.

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

### `srclight_symbols_in_file` — list every symbol in a file

Use this instead of reading a file when the user asks what is inside it.

**Key parameters:**
- `path` — relative file path.
- `project` — required in workspace mode if the path is ambiguous.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_symbols_in_file",
  "arguments": {
    "project": "toolproxy",
    "path": "ToolProxyMCP/Services/McpManager.cs"
  }
}
```

### `srclight_search_symbols` — keyword-oriented symbol search

Best when you have a likely symbol name, code fragment, or short textual clue.

**Key parameters:**
- `query` — symbol name, code fragment, or short natural-language query.
- `kind` — optional filter such as `class`, `method`, or `function`.
- `project` — optional project filter in workspace mode.

**Example — class-only search:**

```json
{
  "server": "srclight",
  "tool": "srclight_search_symbols",
  "arguments": {
    "project": "toolproxy",
    "query": "McpManager",
    "kind": "class"
  }
}
```

### `srclight_hybrid_search` — combined keyword and semantic search

The best default when you want strong recall without giving up exact matches.

**Key parameters:**
- `query` — search query, exact or conceptual.
- `kind` — optional symbol kind filter.
- `project` — optional project filter in workspace mode.

**Example — conceptual search with exact-term fallback:**

```json
{
  "server": "srclight",
  "tool": "srclight_hybrid_search",
  "arguments": {
    "project": "toolproxy",
    "query": "authentication logic for MCP requests",
    "kind": "method"
  }
}
```

### `srclight_semantic_search` — meaning-based search

Use this when the user describes behavior or intent but not the vocabulary the code uses.

**Key parameters:**
- `query` — natural-language description of the concept.
- `kind` — optional symbol kind filter.
- `project` — optional project filter in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_semantic_search",
  "arguments": {
    "project": "toolproxy",
    "query": "code that dispatches tool calls to another MCP server",
    "kind": "method"
  }
}
```

### `srclight_get_signature` — fetch just the signature

The cheapest way to understand an API surface before pulling in full bodies.

**Key parameter:**
- `name` — symbol name.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_signature",
  "arguments": {
    "name": "StartAllServersAsync"
  }
}
```

### `srclight_get_symbol` — fetch full symbol details and source

Use this after you've narrowed to the right symbol and actually need the implementation.

**Key parameters:**
- `name` — symbol name.
- `project` — optional project filter in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_symbol",
  "arguments": {
    "project": "toolproxy",
    "name": "StartAllServersAsync"
  }
}
```

## Common workflows

### Finding where something is defined

1. `srclight_search_symbols` or `srclight_hybrid_search` with the likely name.
2. `srclight_get_signature` if you only need the API shape.
3. `srclight_get_symbol` if you need the body.

### Exploring an unfamiliar file

1. `srclight_symbols_in_file` to get the table of contents.
2. `srclight_get_signature` or `srclight_get_symbol` on the interesting entries.

### Searching by concept rather than spelling

1. `srclight_hybrid_search` first.
2. `srclight_semantic_search` if the wording is still too fuzzy or the code likely uses very different terms.

## See also

- **`toolproxy-srclight-workspace`** — project listing, codebase maps, reindexing, embeddings, and build targets.
- **`toolproxy-srclight-impact`** — callers, dependencies, type hierarchies, and test coverage clues.
- **`toolproxy-srclight-history`** — recent changes, blame, hotspots, and current uncommitted work.