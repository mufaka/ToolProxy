---
name: toolproxy-srclight-impact
description: Trace symbol relationships with Srclight — find callers, callees, dependents, tests, inheritance trees, implementors, and platform-specific variants. Use this when the user asks what calls a method, what would break if something changes, what implements an interface, or which tests likely cover a symbol. Prefer this over plain text search for blast-radius and dependency questions in indexed codebases.
---

# Srclight: impact and relationships (via ToolProxy)

Relationship-oriented analysis for answering "what depends on this, what does it depend on, and what variants or tests surround it?"

## Operating principles for impact analysis

- **Pick the direction deliberately.** `srclight_get_callers` answers who depends on a symbol; `srclight_get_callees` answers what the symbol itself uses.
- **Use `srclight_get_dependents` for blast radius, not just direct callers.** It is the better fit for "what breaks if I change this?" especially with `transitive: true`.
- **Tests are heuristic unless the graph says otherwise.** `srclight_get_tests_for` is strong guidance, not a formal proof of coverage.
- **Type relationships are separate from call relationships.** Use `srclight_get_type_hierarchy` and `srclight_get_implementors` for object model questions, not caller tools.
- **Platform variants matter before cross-platform changes.** Check `srclight_get_platform_variants` if the project has platform-specific implementations.

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

### `srclight_get_callers` — find who calls or references a symbol

Use this for "who calls this?" or "where is this used?"

**Key parameters:**
- `symbol_name` — target symbol.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_callers",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync"
  }
}
```

### `srclight_get_callees` — find what a symbol calls or references

Use this for dependency tracing and understanding how an implementation is composed.

**Key parameters:**
- `symbol_name` — source symbol.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_callees",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync"
  }
}
```

### `srclight_get_dependents` — find symbols affected by a change

This is the blast-radius tool. With `transitive: true`, it walks the dependency chain recursively.

**Key parameters:**
- `symbol_name` — target symbol.
- `transitive` — optional; set true for wider impact.
- `project` — required in workspace mode.

**Example — transitive impact analysis:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_dependents",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync",
    "transitive": true
  }
}
```

### `srclight_get_tests_for` — find likely tests covering a symbol

Useful before edits, especially when you want to run a smaller test slice first.

**Key parameters:**
- `symbol_name` — target symbol.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_tests_for",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "StartAllServersAsync"
  }
}
```

### `srclight_get_type_hierarchy` — inspect inheritance structure

Shows parents and subclasses for a class or struct.

**Key parameters:**
- `name` — class or struct name.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_type_hierarchy",
  "arguments": {
    "project": "toolproxy",
    "name": "McpTool"
  }
}
```

### `srclight_get_implementors` — find concrete implementations of an interface or base type

The right tool for "what classes implement this interface?"

**Key parameters:**
- `interface_name` — interface or base type name.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_implementors",
  "arguments": {
    "project": "toolproxy",
    "interface_name": "ILocalTool"
  }
}
```

### `srclight_get_platform_variants` — find platform-specific variants of a symbol

Use this before changing code that may have Windows/Linux/Apple-specific implementations.

**Key parameters:**
- `symbol_name` — symbol to inspect.
- `project` — required in workspace mode.

**Example:**

```json
{
  "server": "srclight",
  "tool": "srclight_get_platform_variants",
  "arguments": {
    "project": "toolproxy",
    "symbol_name": "CreatePlatformTransport"
  }
}
```

## Common workflows

### Estimating refactor risk

1. `srclight_get_dependents` on the target symbol.
2. `srclight_get_tests_for` to find likely regression coverage.
3. `srclight_get_platform_variants` if the code may differ by platform.

### Understanding a method's role

1. `srclight_get_callers` to see who uses it.
2. `srclight_get_callees` to see what it uses.
3. `srclight_get_dependents` if you need broader impact.

### Understanding a type model

1. `srclight_get_type_hierarchy` for parent/child structure.
2. `srclight_get_implementors` for interface or abstract base realizations.

## See also

- **`toolproxy-srclight-explore`** — search for and inspect the symbols before analyzing relationships.
- **`toolproxy-srclight-history`** — see who changed the relevant code, when, and how often.
- **`toolproxy-srclight-workspace`** — inspect platform conditionals across the whole project, not just near one symbol.