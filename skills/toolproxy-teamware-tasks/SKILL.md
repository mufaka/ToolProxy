---
name: toolproxy-teamware-tasks
description: Work with Teamware tasks — list project tasks, inspect full task details and comments, create new tasks, assign owners, update status, add comments, and review your own assignments. Use this when the user asks what they should work on next, wants to create or triage work, or needs to move a task through the workflow. Prefer this over the project-level skill when the request is about a concrete task rather than project reporting.
---

# Teamware: task management (via ToolProxy)

Task-level work in Teamware: finding work, inspecting it, creating it, and moving it through the workflow.

## Operating principles for task management

- **`my_assignments` first for "what should I do now?"** It is the fastest way to get next actions across all projects.
- **Read the task before mutating it.** `get_task` includes comments and gives the current state before you change status, assignment, or discussion.
- **Filter task lists aggressively.** `list_tasks` can narrow by status, priority, and assignee. Use those filters instead of pulling a noisy whole-project list when the user already gave constraints.
- **Statuses are workflow signals, not notes.** Put rationale, blockers, or handoff detail into `add_comment`; reserve `update_task_status` for the actual state transition.
- **Assignment uses user IDs, not names.** Resolve the right IDs before calling `assign_task`.
- **Task creation is project-scoped.** If you do not know the project ID yet, switch to the projects skill first.

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

### `my_assignments` — show your prioritized assigned work

Use this for "what should I work on next?" or any cross-project personal work queue question.

**Example:**

```json
{
  "server": "teamware",
  "tool": "my_assignments",
  "arguments": {}
}
```

### `list_tasks` — list tasks in a project with filters

Returns project tasks and can be narrowed by status, priority, or assignee.

**Key parameters:**
- `projectId` — required Teamware project ID.
- `status` — optional; `ToDo`, `InProgress`, `InReview`, `Done`, `Blocked`, or `Error`.
- `priority` — optional; `Low`, `Medium`, `High`, or `Critical`.
- `assigneeId` — optional user ID.

**Example — only active high-priority work:**

```json
{
  "server": "teamware",
  "tool": "list_tasks",
  "arguments": {
    "projectId": 42,
    "status": "InProgress",
    "priority": "High"
  }
}
```

### `get_task` — fetch full task details and comments

Use this before making changes, or whenever the user asks for the current state of a specific task.

**Key parameter:**
- `taskId` — required task ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "get_task",
  "arguments": {
    "taskId": 315
  }
}
```

### `create_task` — create a new task in a project

Creates a task with optional description, priority, and due date.

**Key parameters:**
- `projectId` — required project ID.
- `title` — required title.
- `description` — optional supporting detail.
- `priority` — optional; defaults to `Medium`.
- `dueDate` — optional ISO date.

**Example:**

```json
{
  "server": "teamware",
  "tool": "create_task",
  "arguments": {
    "projectId": 42,
    "title": "Add retry handling for webhook delivery failures",
    "description": "Record retry count, exponential backoff, and final failure surface in logs.",
    "priority": "High",
    "dueDate": "2026-05-09"
  }
}
```

### `update_task_status` — move a task through the workflow

Changes a task's status to `ToDo`, `InProgress`, `InReview`, `Done`, `Blocked`, or `Error`.

**Key parameters:**
- `taskId` — required task ID.
- `status` — required new status.

**Example:**

```json
{
  "server": "teamware",
  "tool": "update_task_status",
  "arguments": {
    "taskId": 315,
    "status": "InReview"
  }
}
```

### `add_comment` — add discussion or handoff detail to a task

Use this for progress notes, blockers, review context, or decisions that should travel with the task.

**Key parameters:**
- `taskId` — required task ID.
- `content` — required comment text.

**Example:**

```json
{
  "server": "teamware",
  "tool": "add_comment",
  "arguments": {
    "taskId": 315,
    "content": "Retry flow is implemented locally. I still need to verify the failure metrics in staging."
  }
}
```

### `assign_task` — assign one or more users to a task

Adds assignees to a task using Teamware user IDs.

**Key parameters:**
- `taskId` — required task ID.
- `userIds` — array of user IDs.

**Example:**

```json
{
  "server": "teamware",
  "tool": "assign_task",
  "arguments": {
    "taskId": 315,
    "userIds": ["u_17", "u_21"]
  }
}
```

## Common workflows

### Figuring out what to do next

1. `my_assignments` to get the prioritized queue.
2. `get_task` on the top candidate.
3. `update_task_status` if you are actively picking it up.

### Triage within a project

1. `list_tasks` with status or priority filters.
2. `get_task` for any task you might change.
3. `assign_task`, `add_comment`, or `update_task_status` as needed.

### Creating and handing off a new task

1. `create_task` with enough description to be actionable.
2. `assign_task` if ownership is already known.
3. `add_comment` if the handoff needs extra context not suited to the description.

## See also

- **`toolproxy-teamware-projects`** — project discovery, summaries, activity, and lounge coordination.
- **`toolproxy-teamware-ideas`** — idea context and idea-linked task rationale.
- **`toolproxy-teamware-inbox`** — capturing loose work before it becomes a task.