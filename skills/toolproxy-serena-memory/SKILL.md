---
name: toolproxy-serena-memory
description: Project-scoped persistent memory via the Serena MCP server. List, read, write, edit, rename, and delete memory notes that persist across sessions for a given project — useful for capturing architecture decisions, gotchas, conventions, and other context that would be expensive to rediscover. Routes through the ToolProxy MCP server's call_external_tool dispatcher.
when_to_use: When starting work on a project (to discover what context is already saved), when you've learned something non-obvious worth preserving for next session, or when existing notes need updating or cleanup. Distinct from the host's own memory system — these notes are scoped to the Serena project, not the host agent.
---

# Serena: project memory (via ToolProxy)

Per-project persistent notes. Memories live alongside the Serena project and survive across sessions. They're a good home for architecture summaries, hard-won gotchas, conventions specific to the codebase, and context that's expensive to rediscover.

## Operating principles for memory

- **List first, read second, write third.** When starting a project session and you suspect saved context exists, call `list_memories` first. Only read memories whose names look relevant. Write only when you've genuinely learned something durable.
- **Memories are project-scoped.** Each Serena-activated project has its own set. Don't expect memory from one project to apply to another.
- **Don't confuse with host memory.** This is Serena's project notebook, separate from the host agent's own memory system (e.g., the auto-memory in `~/.claude/...`). The two coexist; pick the one that fits the scope of what you're saving.
- **Keep memories focused.** Each memory should answer one question or capture one insight. A single sprawling "notes" memory is harder to retrieve usefully than several named ones.
- **Memories drift.** Code changes; memories don't auto-update. Treat saved content as accurate-at-the-time and verify against current code before acting on a stale-looking memory.

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

### `list_memories` — what's saved for this project

Returns the names (and possibly short descriptions) of all memories saved for the active project. Cheap; always the right first call when looking for context.

**Example:**

```json
{
  "server": "serena",
  "tool": "list_memories",
  "arguments": {}
}
```

### `read_memory` — load a specific memory

Returns the full body of a named memory.

**Key parameter:**
- `memory_name` — the name returned by `list_memories`.

**Example:**

```json
{
  "server": "serena",
  "tool": "read_memory",
  "arguments": {
    "memory_name": "tech_stack"
  }
}
```

### `write_memory` — save a new memory

Creates a new memory under the given name with the given content. Will likely overwrite if the name already exists — use `edit_memory` for surgical updates.

**Key parameters:**
- `memory_name` — short, descriptive identifier (lowercase, underscores).
- `content` — the body to save (markdown is fine).

**Example:**

```json
{
  "server": "serena",
  "tool": "write_memory",
  "arguments": {
    "memory_name": "mcp_transport_quirks",
    "content": "# MCP transport quirks\n\n- Streamable HTTP falls back to SSE if the server doesn't advertise streaming.\n- STDIO working dir is currently hardcoded to the user home; pass absolute paths for any file args."
  }
}
```

### `edit_memory` — update part of an existing memory

Modifies an existing memory in place. Useful for appending or for surgical edits without rewriting the whole content.

**Key parameters:** vary by Serena version; commonly `memory_name` plus some pattern/replacement or append directive. Verify against the live tool schema.

**Example — append to an existing memory:**

```json
{
  "server": "serena",
  "tool": "edit_memory",
  "arguments": {
    "memory_name": "mcp_transport_quirks",
    "content": "\n- HTTP transport requires explicit `Accept: application/json, text/event-stream` from the client."
  }
}
```

### `rename_memory` — change a memory's name

Renames an existing memory without touching its content.

**Key parameters:**
- `memory_name` — the current name.
- `new_name` — the desired name.

**Example:**

```json
{
  "server": "serena",
  "tool": "rename_memory",
  "arguments": {
    "memory_name": "mcp_transport_quirks",
    "new_name": "mcp_transport_notes"
  }
}
```

### `delete_memory` — remove a memory

Permanently removes a memory from the project.

**Key parameter:**
- `memory_name` — the memory to delete.

**Example:**

```json
{
  "server": "serena",
  "tool": "delete_memory",
  "arguments": {
    "memory_name": "outdated_design_notes"
  }
}
```

## Common workflows

### Surveying saved context at the start of a session

1. `list_memories` to see what's available.
2. For any memory whose name suggests relevance to the task at hand, `read_memory`.
3. Skip irrelevant ones — don't read everything.

### Saving a hard-won insight

1. Decide whether the insight is project-scoped (use this skill) or broader (use the host's memory system).
2. Pick a focused name — `auth_session_lifecycle` beats `notes`.
3. `write_memory` with a markdown body.

### Updating outdated memory

1. `read_memory` to confirm what's there.
2. If the change is small, `edit_memory`.
3. If the memory is structurally wrong or no longer relevant, `delete_memory` and `write_memory` a fresh version.

### Cleanup pass

1. `list_memories`.
2. For each, decide: still accurate? still useful? if no, `delete_memory`. If the name is misleading, `rename_memory`.

## See also

- **`toolproxy-serena-session`** — for activating a project (memories are project-scoped, so the right project must be active).
- **`toolproxy-serena-explore`** — for verifying that a memory's claims still match current code before acting on it.
