---
name: toolproxy-teamware-ideas
description: Explore and discuss Teamware ideas — list ideas with filters, inspect full idea bodies and implementation plans, browse categories, read or post idea comments, and pull idea context linked to a task. Use this when the user asks about feature ideas, specifications, planning context, or a task references an originating idea. Prefer this over task-only views when the goal is to understand the why behind the work.
---

# Teamware: ideas and specifications (via ToolProxy)

Idea discovery, idea discussion, and task-to-idea context. These tools help answer "why does this task exist, what was proposed, and how was it planned?"

## Operating principles for ideas

- **Start from the task when an origin is mentioned.** If a task description contains `Origin: Idea #N`, call `list_ideas_for_task` first. It returns the idea body, specification, implementation plan, and peer tasks promoted from the same idea.
- **Use `list_ideas` to discover; `get_idea` to read.** `list_ideas` is the filtered directory. `get_idea` is the full record for one idea.
- **Filter hard when the idea set is large.** Status, category, effort, fit, and free-text search are there to reduce noise.
- **Read discussion before replying.** `list_idea_comments` first, then `comment_on_idea` if you need to add something new.
- **Categories are project-scoped vocabulary.** Use `list_idea_categories` when you need to interpret or apply category IDs correctly.

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "teamware",
  "tool": "<tool name>",
  "arguments": { /* tool-specific parameters */ }
}
```

Every example below repeats the full envelope so it can be copied without inferring structure from elsewhere.

## Tools

### `list_ideas` — list ideas in a project with filters

The discovery tool for idea work. Supports filtering by status, category, effort, fit, and free-text search.

**Key parameters:**
- `projectId` — required project ID.
- `status` — optional comma-separated idea statuses.
- `categoryId` — optional category ID.
- `effort` — optional; `Unknown`, `S`, `M`, `L`, or `XL`.
- `fit` — optional; `Unknown`, `High`, `Medium`, or `Low`.
- `search` — optional free-text query.

**Example — open product ideas matching a keyword:**

```json
{
  "server": "teamware",
  "tool": "list_ideas",
  "arguments": {
    "projectId": 42,
    "status": "Accepted,Specified,Planned",
    "search": "dashboard"
  }
}
```

### `get_idea` — fetch one idea with body, spec, and plan

Use this when the user wants the full substance of a specific idea.

**Key parameter:**
- `ideaId` — required idea ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "get_idea",
  "arguments": {
    "ideaId": 88
  }
}
```

### `list_idea_categories` — list the project's idea categories

Useful when you need to translate category IDs into names or help the user filter ideas correctly.

**Key parameter:**
- `projectId` — required project ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "list_idea_categories",
  "arguments": {
    "projectId": 42
  }
}
```

### `list_idea_comments` — read idea discussion

Returns the current comment thread for an idea.

**Key parameter:**
- `ideaId` — required idea ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "list_idea_comments",
  "arguments": {
    "ideaId": 88
  }
}
```

### `comment_on_idea` — add a comment to an idea

Supports markdown and `@mentions`. Use it for idea discussion, clarification, or review feedback.

**Key parameters:**
- `ideaId` — required idea ID.
- `body` — markdown comment text.

**Example:**

```json
{
  "server": "teamware",
  "tool": "comment_on_idea",
  "arguments": {
    "ideaId": 88,
    "body": "This should probably ship behind a feature flag first. @alex can you confirm whether rollout needs per-tenant gating?"
  }
}
```

### `list_ideas_for_task` — pull full idea context for a task

This is the highest-leverage Teamware read for tasks that came from an idea. It returns the idea body, specification, implementation plan, and related peer tasks with status.

**Key parameter:**
- `taskId` — required task ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "list_ideas_for_task",
  "arguments": {
    "taskId": 315
  }
}
```

## Common workflows

### Understanding why a task exists

1. `get_task` from the tasks skill if needed to confirm the task reference.
2. `list_ideas_for_task` to load the originating idea, spec, plan, and peer tasks.
3. `get_idea` only if you need the standalone idea record afterward.

### Reviewing the backlog of ideas

1. `list_idea_categories` to understand the vocabulary.
2. `list_ideas` with status or category filters.
3. `get_idea` on the most relevant results.

### Joining an idea discussion

1. `list_idea_comments` to see the current thread.
2. `comment_on_idea` with the next concrete contribution.

## See also

- **`toolproxy-teamware-tasks`** — task execution, comments, and status changes.
- **`toolproxy-teamware-projects`** — project summaries and lounge coordination.
- **`toolproxy-teamware-inbox`** — capturing work before it is formalized into ideas or tasks.