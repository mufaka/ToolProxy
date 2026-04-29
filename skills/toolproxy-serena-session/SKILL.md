---
name: toolproxy-serena-session
description: Run first-time Serena onboarding for a project that has never been onboarded — index symbols, set up language services. Use this skill only when starting work in a brand-new project where Serena's other code-intelligence tools are failing because onboarding hasn't been performed, or when the user explicitly asks "is this project onboarded?" or "set up Serena for this project." Most sessions never need this skill at all — it covers a one-time-per-project setup step, not anything done repeatedly. Reach for it only when something is broken in a way that points to onboarding, never preemptively.
---

# Serena: project onboarding (via ToolProxy)

First-time setup tools for using Serena in a project that has never been onboarded. Almost every session skips this skill entirely — onboarding runs once per project, not once per session.

## Operating principles for onboarding

- **This skill is for first-time setup, not normal operation.** If Serena's exploration and editing tools work, onboarding has already been done; don't re-check.
- **Onboarding is one-time per project.** Once `check_onboarding_performed` reports true, it stays true. Re-running `onboarding` provides no benefit.
- **The signal to use this skill** is another Serena tool failing in a way that points to missing onboarding. Outside that, stay out of it.
- **Onboarding can be slow** on large codebases — symbol indexing across the whole project. Set the user's expectation before kicking it off.

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

### `check_onboarding_performed` — has this project been set up?

Returns whether the active project has completed Serena onboarding. Cheap; safe to call without side effects.

**Example:**

```json
{
  "server": "serena",
  "tool": "check_onboarding_performed",
  "arguments": {}
}
```

### `onboarding` — set up a project for Serena

Performs first-time onboarding: indexes the project's symbols, sets up language services, etc. Only needs to run once per project. Can be slow on large codebases.

**Example:**

```json
{
  "server": "serena",
  "tool": "onboarding",
  "arguments": {}
}
```

## Common workflows

### Bootstrapping a brand-new project

1. `check_onboarding_performed` — if true, skip the rest; the project is ready.
2. If false, surface to the user that onboarding is needed and may take a moment on large codebases.
3. `onboarding` to perform the one-time setup.
4. Once complete, the explore / edit / memory skills work normally.

### Diagnosing "no active project" errors from other Serena tools

If exploration or editing tools fail with a "no active project" error, this is usually a proxy or upstream config issue, not something this skill can fix. Surface the situation to the user — the proxy's `appsettings.json` or its upstream Serena configuration likely needs adjustment. `check_onboarding_performed` can confirm whether onboarding is the missing piece, but if the underlying error specifically says "no active project," onboarding is not what's wrong.

## See also

- **`toolproxy-serena-explore`** — once a project is onboarded, the day-to-day tool for code discovery.
- **`toolproxy-serena-edit`** — for code modification once onboarding is complete.
- **`toolproxy-serena-memory`** — for reading or writing project-scoped notes; `list_memories` there gives the memories list directly, no session-state call needed.
