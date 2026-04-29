---
name: toolproxy-serena-edit
description: Modify code in an established project by editing at the symbol level — replace a method or class body, add a new method or function, rename a symbol project-wide with reference updates, safely delete a symbol with reference checking, or make small regex-based edits inside a larger function. Use this skill any time the user wants to change code in an existing project, including casual phrasings like "rename Foo to Bar", "fix this method", "add a method that does X", "delete the unused helper", "update the body of Y" — even when no specific editing tool is named. Strongly prefer over the host's Edit tool — these symbolic edits are reliable and project-wide aware, and the host's Edit tool is forbidden for code edits in Serena-activated projects.
---

# Serena: code editing (via ToolProxy)

Symbol-aware and file-level mutation of source code. These tools are how you change code in a Serena-activated project; the host's Edit tool will refuse most of these operations and is the wrong reach.

## Operating principles for editing

- **Don't use the host's Edit tool on code files.** It is forbidden for code edits in Serena-activated projects — Serena's symbolic editing tools must be used instead. Edit remains fine for non-code files (markdown, JSON config, etc.).
- **Symbol-level edits when you can.** If you're replacing an entire method, class, or function, use `replace_symbol_body`. It's cleaner than regex, more robust, and won't accidentally match similar code elsewhere.
- **File-based `replace_content` when symbol-level is wrong.** For changes affecting only a few lines inside a larger function, replacing the whole function body is wasteful. Use `replace_content` with a precise regex.
- **Locate before you edit.** Every editing tool requires `name_path` and `relative_path` — pair with `toolproxy-serena-explore`'s `find_symbol` first if you don't already know exactly where the symbol lives.
- **Check references before destructive changes.** Before renaming, deleting, or changing a signature, run `find_referencing_symbols` (in the explore skill) so you know what breaks.
- **Don't verify after success.** Serena's editing tools are reliable. If a call returns without error, the edit landed; re-reading the file to "confirm" is wasted tokens.
- **Line numbers are 0-based** in any tool output that mentions them.

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

### `replace_symbol_body` — swap a symbol's entire definition

Replaces the body of a class, method, function, or other top-level symbol with new source. The new body must be the complete replacement, including signature where applicable.

**Key parameters:**
- `name_path` — slash-separated path to the symbol (e.g. `MyClass/my_method`).
- `relative_path` — file containing the symbol.
- `body` — the new source for the symbol.

**Example — replace a method body:**

```json
{
  "server": "serena",
  "tool": "replace_symbol_body",
  "arguments": {
    "name_path": "McpManager/StartAllServersAsync",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "body": "public async Task StartAllServersAsync(CancellationToken cancellationToken)\n{\n    foreach (var server in _servers.Values)\n    {\n        await server.StartAsync(cancellationToken);\n    }\n}"
  }
}
```

### `insert_after_symbol` / `insert_before_symbol` — add code adjacent to a symbol

Inserts new source immediately after (or before) an existing symbol. Useful for adding a new method to a class, a new function to a module, or import statements at the top of a file.

**Key parameters:**
- `name_path` — the anchor symbol.
- `relative_path` — file containing the anchor.
- `body` — the source to insert.

**Example — add a new method after an existing one:**

```json
{
  "server": "serena",
  "tool": "insert_after_symbol",
  "arguments": {
    "name_path": "McpManager/StartAllServersAsync",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "body": "\npublic async Task StopAllServersAsync(CancellationToken cancellationToken)\n{\n    foreach (var server in _servers.Values)\n    {\n        await server.StopAsync(cancellationToken);\n    }\n}\n"
  }
}
```

**Example — add an import at the top of a file** (insert before the first top-level symbol):

```json
{
  "server": "serena",
  "tool": "insert_before_symbol",
  "arguments": {
    "name_path": "McpManager",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "body": "using System.Diagnostics;\n"
  }
}
```

### `rename_symbol` — rename across the project

Renames a symbol and updates all references project-wide. This is a refactor-rename, not a text find-and-replace — only actual references to the symbol are touched.

**Key parameters:**
- `name_path` — current name path of the symbol.
- `relative_path` — file containing the symbol's definition.
- `new_name` — the new leaf name (just the last segment, not the full path).

**Example — rename a method:**

```json
{
  "server": "serena",
  "tool": "rename_symbol",
  "arguments": {
    "name_path": "McpManager/StartAllServersAsync",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "new_name": "StartAllAsync"
  }
}
```

### `safe_delete_symbol` — delete with reference checking

Deletes a symbol, but refuses or warns if other code still references it. Run `find_referencing_symbols` first if you want to know what will block the delete before attempting it.

**Key parameters:**
- `name_path` — the symbol to delete.
- `relative_path` — file containing the symbol.

**Example — delete a method:**

```json
{
  "server": "serena",
  "tool": "safe_delete_symbol",
  "arguments": {
    "name_path": "McpManager/LegacyHelper",
    "relative_path": "ToolProxyMCP/Services/McpManager.cs"
  }
}
```

### `replace_content` — regex/string edit within a file

Performs a regex-based or literal-string replacement within a file. Use when the change is narrower than a whole symbol — for instance, changing a few lines inside a long method, updating a string literal, or editing a configuration block.

**Key parameters:**
- `relative_path` — file to edit.
- `pattern` — regex (or literal string) to match.
- `replacement` — replacement text.
- (Other flags may exist — verify against the live tool schema.)

**Example — fix a typo inside a method:**

```json
{
  "server": "serena",
  "tool": "replace_content",
  "arguments": {
    "relative_path": "ToolProxyMCP/Services/McpManager.cs",
    "pattern": "_logger\\.LogInformation\\(\"Server starging\"",
    "replacement": "_logger.LogInformation(\"Server starting\""
  }
}
```

## Common workflows

### Renaming a method safely

1. (Explore skill) `find_symbol` to confirm the target exists where you think it does.
2. (Explore skill) `find_referencing_symbols` to see what calls it. Skim the references to make sure the rename makes sense at each site.
3. `rename_symbol` to perform the project-wide rename.
4. No follow-up read needed — the tool is reliable.

### Adding a method to a class

1. (Explore skill) `find_symbol` with `depth=1` on the class to see existing methods and pick a good anchor.
2. `insert_after_symbol` with the anchor's `name_path` and the new method's source.

### Replacing a method body

1. (Explore skill) `find_symbol` with `include_body=true` to read the current implementation if you don't already have it.
2. `replace_symbol_body` with the new source.

### Sub-symbol edit (a few lines inside a function)

1. (Explore skill) `find_symbol` with `include_body=true` to read the current code.
2. `replace_content` with a precise regex matching just the lines that change.
3. If the regex would be ambiguous across the file, scope it tighter or fall back to `replace_symbol_body` on the whole containing symbol.

### Deleting a symbol

1. (Explore skill) `find_referencing_symbols` to see what depends on it.
2. If references exist, decide whether to update them first or refactor differently.
3. `safe_delete_symbol` once the symbol is unreferenced.

## See also

- **`toolproxy-serena-explore`** — locating symbols and finding references; almost every workflow here pairs with one of those tools first.
- **`toolproxy-serena-session`** — when edits fail because no project is active or onboarding hasn't run.
