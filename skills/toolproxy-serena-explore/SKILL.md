---
name: toolproxy-serena-explore
description: Locate classes, functions, methods, and other code symbols by name in an established codebase; survey what's inside a file or directory; and trace references and call sites — all without reading whole files. Use this skill any time the user asks where a symbol is defined, what's in a file, what calls a function, or otherwise wants to navigate or understand code structure, even when they don't name any specific tool ("where is X", "show me the Foo class", "what uses this method", "what's in McpManager.cs"). Strongly prefer over the host's Read tool for code discovery in any non-trivial codebase — dramatically more token-efficient and surfaces structure Read cannot.
---

# Serena: code exploration (via ToolProxy)

Symbol-aware, semantic exploration of a codebase. These tools answer "what is in this code, where is it, and how do the pieces relate?" without pulling whole files into context.

## Operating principles for exploration

- **Symbols, not files.** Prefer symbol-aware queries over reading whole files. A 1,500-line file usually has a handful of symbols you actually care about — load just those.
- **Don't fetch bodies until you need them.** All exploration tools support `include_body=false` (the default). Get an overview first, decide what matters, then re-query with `include_body=true` for specific symbols.
- **Scope your queries.** Pass `relative_path` to limit a search to a single file or directory. Unscoped searches across an entire project are slower, noisier, and rarely what you want.
- **Line numbers are 0-based** in Serena's output. Don't translate without checking.
- **Don't use the host's Read tool to explore code.** Read pulls in everything; these tools pull only what you ask for. Read remains fine for non-code files, or for fully reading a small file you've already narrowed down to.

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "serena",
  "tool": "<tool name>",
  "arguments": { /* tool-specific parameters */ }
}
```

Every example below repeats the full envelope so it can be copied without inferring structure from elsewhere.

## Tools

### `get_symbols_overview` — what's in this file or directory

Returns the top-level symbols (classes, functions, etc.) in a file or directory, without their bodies. Cheap. Always the right first move when looking at unfamiliar code.

**Key parameter:**
- `relative_path` — file or directory to inventory.

**Example — survey a single file:**

```json
{
  "server": "serena",
  "tool": "get_symbols_overview",
  "arguments": {
    "relative_path": "ToolProxyMCP/Services/McpManager.cs"
  }
}
```

**Example — survey a directory:**

```json
{
  "server": "serena",
  "tool": "get_symbols_overview",
  "arguments": {
    "relative_path": "ToolProxyMCP/Services"
  }
}
```

### `find_symbol` — locate a specific symbol

Finds a symbol by **name path**. Name paths are slash-separated: `ClassName`, `ClassName/method_name`, `Outer/Inner/method`. The last segment is the leaf you want.

**Key parameters:**
- `name_path` — slash-separated symbol path.
- `relative_path` — optional; scope to a file or directory. Strongly recommended when known.
- `include_body` — default `false`. Set `true` only when you need the actual source.
- `depth` — default `0`. Set higher to also fetch the symbol's children at overview level (e.g., `depth=1` on a class returns its methods).

**Example — find a class and list its methods, without bodies:**

```json
{
  "server": "serena",
  "tool": "find_symbol",
  "arguments": {
    "name_path": "McpManager",
    "relative_path": "ToolProxyMCP/Services",
    "include_body": false,
    "depth": 1
  }
}
```

**Example — read a specific method's body:**

```json
{
  "server": "serena",
  "tool": "find_symbol",
  "arguments": {
    "name_path": "McpManager/StartAllServersAsync",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "include_body": true
  }
}
```

### `find_referencing_symbols` — find call sites and references

Returns symbols that reference a target symbol, with code snippets around each reference site.

**When to reach for it:** before renaming, deleting, or changing a symbol's signature; when tracing how something is used; when answering "where is this called from?"

**Key parameters:**
- `name_path` — the symbol whose references you want.
- `relative_path` — file containing the target symbol's definition.

**Example — find callers of a method:**

```json
{
  "server": "serena",
  "tool": "find_referencing_symbols",
  "arguments": {
    "name_path": "McpManager/StartAllServersAsync",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs"
  }
}
```

## Common workflows

### Surveying an unfamiliar file

1. `get_symbols_overview` on the file → list of top-level symbols.
2. Pick the symbol(s) of interest.
3. `find_symbol` with `depth=1, include_body=false` to see their structure.
4. `find_symbol` with `include_body=true` on specific symbols you need to read.

### Tracing usage of a symbol

1. `find_symbol` to confirm the symbol's location and signature.
2. `find_referencing_symbols` to enumerate callers.
3. For each interesting caller, `find_symbol` with `include_body=true` to read the surrounding code.

### Deciding whether a refactor is safe

1. `find_referencing_symbols` on the target.
2. If references are few and local, the refactor is contained.
3. If references span many files or appear in tests, plan accordingly before reaching for the edit tools.

## See also

- **`toolproxy-serena-edit`** — modifying code at the symbol level (rename, replace body, insert before/after).
- **`toolproxy-serena-session`** — activating a project or fetching session-specific state when Serena isn't responding as expected.
