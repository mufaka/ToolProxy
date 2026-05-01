---
name: toolproxy-teamware-inbox
description: Capture rough work into Teamware's inbox and turn it into tracked tasks later — review unprocessed inbox items, add new captures, and process an inbox item into a project task with priority and triage flags. Use this when the user wants quick capture without full task authoring, or when they are doing inbox review and deciding what becomes a next action versus someday/maybe.
---

# Teamware: inbox capture and triage (via ToolProxy)

Fast capture first, structured task creation second. These tools are for loose incoming work that is not ready to become a fully managed task yet.

## Operating principles for inbox work

- **Capture now, organize later.** `capture_inbox` is for preserving work before details are complete.
- **`my_inbox` only shows unprocessed items.** Once an item has been turned into a task, it drops out of this queue.
- **Processing is the handoff into real project work.** `process_inbox_item` creates the task in a chosen project and lets you set priority plus triage hints.
- **Choose triage flags deliberately.** `isNextAction` and `isSomedayMaybe` describe opposite intent; set them on purpose, not by habit.
- **Know the target project before processing.** If the right project is unclear, switch to the projects skill first rather than guessing.

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

### `my_inbox` — list unprocessed inbox items

Use this for inbox review sessions or whenever the user asks what has been captured but not yet turned into tasks.

**Example:**

```json
{
  "server": "teamware",
  "tool": "my_inbox",
  "arguments": {}
}
```

### `capture_inbox` — add a new inbox item

Creates a lightweight inbox record with a title and optional description.

**Key parameters:**
- `title` — required title.
- `description` — optional supporting detail.

**Example:**

```json
{
  "server": "teamware",
  "tool": "capture_inbox",
  "arguments": {
    "title": "Investigate intermittent 502s from staging webhook endpoint",
    "description": "Happened twice during yesterday's deployment window. Need logs and repro steps."
  }
}
```

### `process_inbox_item` — convert an inbox item into a task

Processes an inbox item into a task in a chosen project. This is where quick capture becomes scheduled work.

**Key parameters:**
- `inboxItemId` — required inbox item ID.
- `projectId` — required target project ID.
- `priority` — required task priority.
- `isNextAction` — optional; mark as a next action.
- `isSomedayMaybe` — optional; mark as someday/maybe.

**Example — process into an active task:**

```json
{
  "server": "teamware",
  "tool": "process_inbox_item",
  "arguments": {
    "inboxItemId": 27,
    "projectId": 42,
    "priority": "High",
    "isNextAction": true
  }
}
```

**Example — park it as someday/maybe:**

```json
{
  "server": "teamware",
  "tool": "process_inbox_item",
  "arguments": {
    "inboxItemId": 31,
    "projectId": 42,
    "priority": "Low",
    "isSomedayMaybe": true
  }
}
```

## Common workflows

### Quick capture during active work

1. `capture_inbox` with just enough detail not to lose the thought.
2. Return to the current task instead of expanding scope immediately.

### Inbox review session

1. `my_inbox` to load the unprocessed queue.
2. Decide the target project and urgency for each item.
3. `process_inbox_item` into either a next action or someday/maybe task.

### Turning vague work into a task safely

1. `my_inbox` to confirm the item is still unprocessed.
2. `list_projects` from the projects skill if the project choice is unclear.
3. `process_inbox_item` with the right project and priority.

## See also

- **`toolproxy-teamware-projects`** — find the right project before processing inbox items.
- **`toolproxy-teamware-tasks`** — work with the task after processing creates it.
- **`toolproxy-teamware-ideas`** — use when the captured item needs idea-level discussion before execution.