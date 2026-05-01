---
name: toolproxy-teamware-projects
description: Get high-level situational awareness in Teamware — list projects, inspect project details and summaries, review recent activity, and read, search, or post lounge messages. Use this when the user asks what projects exist, what changed recently, what the current state of a project is, or wants to coordinate through the lounge. Prefer this over jumping straight into task lists when the goal is orientation, reporting, or team communication.
---

# Teamware: projects and collaboration (via ToolProxy)

Project-level visibility and lightweight team coordination. These tools answer "what projects do we have, what is happening in them, and what has been said about them?"

## Operating principles for projects and collaboration

- **Start with the project list.** If the user has not named a project yet, `list_projects` is the first move. Everything else hangs off a project ID.
- **Use summaries before raw activity when counts are enough.** `get_project_summary` gives a quick pulse; `get_activity` is for the actual log stream.
- **Pick the right project view.** `get_project` is the detailed record with task statistics; `get_project_summary` is the lighter dashboard view for a specific period.
- **Lounge tools split by intent.** `list_lounge_messages` is for recency, `search_lounge_messages` is for content lookup, and `post_lounge_message` is for broadcasting something new.
- **Omitting `projectId` means global lounge or cross-project activity where supported.** Do that deliberately; don't accidentally search the global lounge when the question is project-specific.
- **Activity defaults to `this_week`.** Set `today` or `this_month` when the timeframe matters.

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

### `list_projects` — list every project you can access

The right first call when the user has not named a project yet, or when you need to map a project name to its Teamware ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "list_projects",
  "arguments": {}
}
```

### `get_project` — fetch detailed project information

Returns the detailed project record, including task statistics. Use it when the user wants the current state of one project rather than a cross-project overview.

**Key parameter:**
- `projectId` — required Teamware project ID.

**Example:**

```json
{
  "server": "teamware",
  "tool": "get_project",
  "arguments": {
    "projectId": 42
  }
}
```

### `get_project_summary` — compact project dashboard for a time period

Returns task statistics plus activity counts for `today`, `this_week`, or `this_month`. Use it for status checks and short progress reports.

**Key parameters:**
- `projectId` — required Teamware project ID.
- `period` — optional; defaults to `this_week`.

**Example — weekly summary:**

```json
{
  "server": "teamware",
  "tool": "get_project_summary",
  "arguments": {
    "projectId": 42,
    "period": "this_week"
  }
}
```

### `get_activity` — retrieve activity log entries

Use this when the user wants the actual recent events, not just counts. Can be scoped to a project or left unscoped for the authenticated user's activity across all projects.

**Key parameters:**
- `projectId` — optional; omit for user-wide activity.
- `period` — optional; `today`, `this_week`, or `this_month`.

**Example — project activity today:**

```json
{
  "server": "teamware",
  "tool": "get_activity",
  "arguments": {
    "projectId": 42,
    "period": "today"
  }
}
```

### `list_lounge_messages` — read recent lounge messages

Shows the latest messages in a project's lounge or, when `projectId` is omitted, the global lounge.

**Key parameters:**
- `projectId` — optional; omit for the global lounge.
- `count` — optional; defaults to 20.

**Example — recent project lounge messages:**

```json
{
  "server": "teamware",
  "tool": "list_lounge_messages",
  "arguments": {
    "projectId": 42,
    "count": 10
  }
}
```

### `search_lounge_messages` — find messages by content

Use this when you need to locate a past decision, mention, or keyword in the lounge history.

**Key parameters:**
- `query` — text to match against message content.
- `projectId` — optional; omit for the global lounge.

**Example — search a project lounge:**

```json
{
  "server": "teamware",
  "tool": "search_lounge_messages",
  "arguments": {
    "projectId": 42,
    "query": "deployment rollback"
  }
}
```

### `post_lounge_message` — send a message to a lounge

Use this for coordination, handoff notes, or quick announcements. Supports project lounge posts and global lounge posts.

**Key parameters:**
- `content` — the message body.
- `projectId` — optional; omit for the global lounge.

**Example — post to a project lounge:**

```json
{
  "server": "teamware",
  "tool": "post_lounge_message",
  "arguments": {
    "projectId": 42,
    "content": "Build fix is merged. Please pull latest before resuming test work."
  }
}
```

## Common workflows

### Getting oriented in Teamware

1. `list_projects` to find the relevant project and ID.
2. `get_project_summary` for a quick health check.
3. `get_activity` if the user wants to know what actually changed.

### Investigating a past discussion

1. `search_lounge_messages` with the keyword or phrase.
2. `list_lounge_messages` around the same project if you need nearby context.
3. `post_lounge_message` only if you need to follow up publicly.

### Writing a short project status update

1. `get_project_summary` for counts.
2. `get_activity` for noteworthy recent changes.
3. `post_lounge_message` to publish the summary.

## See also

- **`toolproxy-teamware-tasks`** — task triage, creation, assignment, comments, and status updates.
- **`toolproxy-teamware-ideas`** — idea discovery, idea discussion, and task-to-idea context.
- **`toolproxy-teamware-inbox`** — quick capture and inbox-to-task processing.