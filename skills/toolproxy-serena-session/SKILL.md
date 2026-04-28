---
name: toolproxy-serena-session
description: Session and project setup for the Serena MCP server — fetch session-specific runtime state (active project, available memories, programming languages) and run first-time project onboarding. Routes through the ToolProxy MCP server's call_external_tool dispatcher.
when_to_use: When Serena's other tools fail with "no active project" errors, when you need to confirm which project is currently activated, when starting work on a brand-new project that hasn't been onboarded to Serena yet, or when you specifically need the runtime state (memories list, languages) that initial_instructions returns. Most sessions don't need this skill — reach for it only when a Serena tool is misbehaving or when first-time setup is required.
---

# Serena: session & onboarding (via ToolProxy)

One-time-per-session and one-time-per-project setup tools. Most of the time you can ignore this skill — Serena's exploration and editing tools work without explicitly fetching session state. Reach for these tools when something specific isn't working, or when starting on a project Serena has never seen.

## Operating principles for session tools

- **Don't call these reflexively.** `initial_instructions` returns a sizeable payload (philosophy guidance, tool usage rules, runtime state). The static portion of that guidance is already covered by the other Serena skills — calling it preemptively just to "be safe" wastes tokens.
- **Static vs. dynamic content.** The Serena philosophy and tool usage rules ("symbols not files," "0-based line numbers," etc.) are static and live in the explore/edit/memory skills. The dynamic portion of `initial_instructions` — active project name, memories list, programming languages — is what's actually unique per session.
- **Onboarding is a one-time cost.** Per project, not per session. Once a project has been onboarded, `check_onboarding_performed` will report true forever and `onboarding` doesn't need to be called again.
- **If a Serena tool errors with "no active project,"** that's the signal to use this skill. Otherwise stay out of it.

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

### `initial_instructions` — fetch session-specific state

Returns Serena's full instruction manual plus runtime state for the current session: the active project's name and path, the list of saved memory names, and the programming languages Serena recognizes for that project.

**When to call:** when you need the runtime state. The static guidance portion is duplicative of these skills; lean on the runtime fields.

**Example:**

```json
{
  "server": "serena",
  "tool": "initial_instructions",
  "arguments": {}
}
```

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

Performs first-time onboarding: index the project's symbols, set up language services, etc. Only needs to run once per project. Can be slow on large codebases.

**Example:**

```json
{
  "server": "serena",
  "tool": "onboarding",
  "arguments": {}
}
```

## Common workflows

### Recovering from a "no active project" error

1. `initial_instructions` to confirm what (if anything) is currently activated.
2. If no project is active, the proxy or upstream config needs adjustment — this isn't something the agent fixes alone. Surface the situation to the user.

### Bootstrapping a brand-new project

1. `check_onboarding_performed` — if true, skip the rest.
2. If false, `onboarding` to perform the one-time setup.
3. Once complete, the explore/edit/memory skills will work normally.

### Confirming what memories exist (alternative to the memory skill)

If you only want the *list* of memories — not to read them — `initial_instructions` already includes that list and avoids a second call.

```json
{
  "server": "serena",
  "tool": "initial_instructions",
  "arguments": {}
}
```

(For reading memory contents, use `toolproxy-serena-memory` instead.)

## See also

- **`toolproxy-serena-explore`** — once a project is active and onboarded, this is the day-to-day tool for code discovery.
- **`toolproxy-serena-edit`** — for code modification once the session is healthy.
- **`toolproxy-serena-memory`** — for reading/writing project memories (the list is also surfaced by `initial_instructions` here, but reading bodies requires the memory skill).
