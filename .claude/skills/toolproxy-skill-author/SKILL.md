---
name: toolproxy-skill-author
description: Author one or more ToolProxy skills (SKILL.md files) for a connected upstream MCP server — group its tools into capability areas, draft frontmatter and body that follow the existing toolproxy-* house style, and add them to ToolProxy's bundled skills payload under <repo>/skills/ so they ship to every host that runs install_skills. Use this skill when the user asks to "create a ToolProxy skill" / "author skills for the [X] MCP server" / "add a skill for [server]" / "wrap the connected [X] server in a skill" — anything that asks for new SKILL.md files describing how the agent should call upstream tools through ToolProxy. Output is markdown files in the ToolProxy repo's bundled payload, not changes to the proxy's C# code.
---

# ToolProxy: skill author

Authoring helper for writing new ToolProxy skills against MCP servers connected to this project. Output is one or more `SKILL.md` files placed under `<repo>/skills/<skill-name>/` — the bundled payload that the build copies into `bin/.../net10.0/skills/` and that `install_skills` distributes to every client. ToolProxy's C# code is not modified by this skill.

This skill itself lives in `.claude/skills/toolproxy-skill-author/` because it is a working tool for this repo only — but the skills it produces are *shipped* from ToolProxy.

## What you are authoring

A ToolProxy skill is a SKILL.md file that:

- Describes a single **capability area** of one upstream MCP server (e.g. "code exploration", "code editing", "memory") — not the whole server, and not a single tool.
- Tells the host agent **when** to reach for that capability (rich `description` in the frontmatter).
- Shows the agent **how** to dispatch each upstream tool through ToolProxy's `call_external_tool`, with a complete JSON envelope per example.
- Captures **operating principles** — the non-obvious rules of using this server well, taken from the upstream's own docs and the user's experience.

The canonical examples already in this repo are `toolproxy-serena-explore`, `toolproxy-serena-edit`, `toolproxy-serena-memory`, and `toolproxy-serena-session` (one MCP server, four skills, split by capability area). Read at least one before authoring — the house style is enforced by imitation, not by spec.

## Operating principles for authoring

- **One capability area per skill.** If a server has `find_symbol` / `replace_symbol_body` / `list_memories` / `onboarding`, that's four skills, not one. The split is by *what kind of work* the user is doing, not by tool count. A skill the agent never selects is dead weight; an oversized skill that loads unrelated guidance into context is the bloat case ToolProxy was built to avoid.
- **Skip authoring entirely when nothing useful would land in the body.** If a server has a handful of tools whose schemas already speak for themselves and the upstream has no operating principles worth distilling, a skill adds no value over the host's normal tool listing. Tell the user, don't fabricate guidance.
- **The description carries the routing.** It's the only part the host sees during discovery. Lead with what the skill does, then *when* to use it (concrete user phrasings, including casual ones), then any "prefer this over X" guidance. Bad descriptions ("Helps with PDFs.") are why skills don't get selected.
- **Every tool example shows the full envelope.** `{"server": "...", "tool": "...", "arguments": {...}}` — repeated in every code block, even if it feels redundant. The agent copies these blocks out of context; partial envelopes invite mistakes.
- **Match the existing voice.** Terse. Principled. No hedging. Explain *why* a rule exists when it isn't obvious. No bullet-point soup, no "AI-flavored" filler.
- **Don't invent tools or parameters.** If you're not sure a tool exists or what its arguments are, treat that as a blocker — confirm from the upstream's source / docs before writing it down. A SKILL.md that calls a non-existent tool is worse than no skill.
- **Don't paraphrase the upstream's tool description into the body.** Pull out the operating-principles content the upstream *doesn't* surface in tool descriptions. The agent will already see tool descriptions when it dispatches; the skill body should add what those descriptions can't.

## Authoring flow

1. **Identify the server.** Ask the user which connected MCP server this skill is for, and confirm the exact server `Name` from `appsettings.json` — that string is what gets put into the `server` field of every example, and it is case-sensitive.
2. **Enumerate the upstream tools and schemas.** ToolProxy's `list_servers` returns only `name`, `description`, and `tool_count` — *not* the tool list itself. To get tool details, use one of:
   - The upstream server's own README / docs (usually the cleanest).
   - The upstream's source if it's open (search for tool registrations).
   - Running the upstream MCP server directly (outside ToolProxy) and listing its tools.
   - Asking the user to paste the `tools/list` output from a session that has the upstream connected directly.

   You need **name, description, and JSON-schema of arguments** for every tool you'll cover. Don't guess.
3. **Group tools into capability areas.** A capability area is a coherent piece of work the user does ("explore code", "edit code", "manage browser tabs"). One area = one skill. Map every tool you intend to cover into exactly one area; tools that don't cluster naturally either go in their own skill or get dropped.
4. **Decide if the skill is worth writing at all.** If a candidate skill would have nothing to say beyond what the tool descriptions already say, skip it. Better to ship two strong skills than four mediocre ones.
5. **Pick names.** Skill directory names follow the agentskills.io rules (see *Frontmatter constraints* below). House convention for this project is `toolproxy-<server>-<area>` (e.g., `toolproxy-serena-explore`). Lowercase, hyphenated, descriptive of the area. The directory name and the `name` frontmatter field must match exactly.
6. **Draft each SKILL.md** using the template below. Then iterate: re-read against the existing serena skills until the voice matches.
7. **Place the file** at `<repo-root>/skills/<skill-name>/SKILL.md` — the bundled payload directory. The csproj already has `<Content Include="..\skills\**\*" ... />`, so any new file under `skills/` is picked up on the next ToolProxy build with no project-file edits. Do **not** drop the new skill into `.claude/skills/` directly: that bypasses the build pipeline and the skill won't ship to other hosts.

## Frontmatter constraints

From the agentskills.io specification:

| Field         | Required | Rule                                                                              |
| ------------- | -------- | --------------------------------------------------------------------------------- |
| `name`        | Yes      | 1-64 chars, lowercase `a-z`/digits/`-` only. No leading, trailing, or `--`. **Must match parent directory name exactly.** |
| `description` | Yes      | 1-1024 chars. Non-empty. What the skill does *and* when to use it.                |
| `license`     | No       | Skip unless the user asks.                                                        |
| `compatibility` | No     | Skip — these skills are tied to this project's ToolProxy.                         |
| `metadata`    | No       | Skip unless there's a real reason.                                                |
| `allowed-tools` | No     | Skip — experimental, not used by the existing toolproxy-* skills.                 |

For toolproxy skills in this project, only `name` and `description` should appear.

## Description authoring

The `description` field is the most important text in the file — the host loads it into base context for every project session and uses it to decide whether to activate the skill. Treat it as a one-paragraph router.

A strong description has three parts, in this order:

1. **What the skill does**, in concrete terms tied to the upstream server. ("Symbol-aware exploration of a codebase via Serena…")
2. **When to use it**, with several user phrasings — including casual / indirect ones. The host matches loosely; give it surface area. ("…where is X defined", "what calls Foo", "show me the Bar class", "what's in McpManager.cs"…)
3. **Routing guidance** when relevant — "prefer this over the host's Read tool", "don't use this when X", "see also `toolproxy-foo-bar`".

Avoid: marketing language, hedging ("may help with"), and descriptions that only restate the skill name. Aim for ~300-700 characters; the limit is 1024 but you almost never need it.

## Body structure (recommended sections)

Match the existing serena skills:

```
# <Server>: <capability area> (via ToolProxy)

<one or two sentences framing what this capability area covers>

## Operating principles for <area>

- ...
- ...

## How to invoke

<note that everything goes through call_external_tool, with the canonical envelope>

## Tools

### `tool_name` — one-line purpose

<paragraph: when to reach for it>

**Key parameters:**
- `param_name` — what it does, defaults, when to set it.

**Example — <concrete scenario>:**

```json
{
  "server": "<exact configured server name>",
  "tool": "tool_name",
  "arguments": { /* ... */ }
}
```

## Common workflows

### <named workflow>

1. Step.
2. Step.
3. Step.

## See also

- **`toolproxy-<other>`** — <one-line pointer>
```

Notes on each section:

- **Operating principles** — the highest-leverage section. Drop the rules that the upstream's tool descriptions can't carry: when one tool obsoletes another, when not to fetch bodies, what counts as "scoped", project-wide conventions, gotchas. If you have nothing here, reconsider whether the skill is worth writing.
- **How to invoke** — always show the full envelope once, name the dispatcher (`call_external_tool`), and state explicitly that every example below repeats the envelope.
- **Tools** — one `###` heading per tool. Lead the heading with the tool name in backticks and a one-line purpose. Don't enumerate every parameter — list only the ones that affect how the agent uses the tool. Always include at least one realistic example, and prefer two when the parameter combinations are meaningfully different.
- **Common workflows** — short, numbered, real. Pull from how the user actually uses the server, not from upstream documentation in the abstract.
- **See also** — cross-link sibling skills (other capability areas of the same server). Helps the host pick the right one.

Keep the whole body under ~500 lines. If a worked walkthrough is too long, link to a `references/<topic>.md` file alongside `SKILL.md` (agentskills.io supports this) instead of inlining it.

## Validation checklist

Before declaring a skill done, verify each:

- [ ] Directory name and `name` frontmatter match exactly.
- [ ] `name` passes the regex (lowercase, digits, hyphens; no leading/trailing/`--`).
- [ ] `description` is ≤ 1024 chars and contains both *what* and *when*.
- [ ] Every tool covered exists on the upstream server (cross-checked against docs/source).
- [ ] Every JSON example uses the exact configured server name (case-sensitive).
- [ ] Every JSON example is valid JSON and uses the full `{server, tool, arguments}` envelope.
- [ ] Operating principles section says something the tool descriptions don't already say.
- [ ] Voice matches the existing `toolproxy-serena-*` skills (terse, principled, no fluff).
- [ ] Body ≤ ~500 lines.

## After authoring

The skill exists at `<repo>/skills/<skill-name>/SKILL.md` but isn't reaching any host yet. To put it in front of the agent for end-to-end testing:

1. **Rebuild ToolProxy** (`dotnet build` from the repo root, or restart `dotnet run`). The MSBuild content rule copies `skills/**` into `bin/.../net10.0/skills/` on every build.
2. **Re-run `install_skills`** from a host session connected to ToolProxy, pointing at this project's `.claude/skills` (or another project's). `install_skills` overwrites existing skill directories unconditionally, so the new skill shows up alongside the existing ones.
3. **Restart the host once** if `.claude/skills/` was just created (the `install_skills` response includes a one-shot warning when this is needed). Subsequent reinstalls are picked up via live reload.
4. **Confirm the host surfaces the skill** in its skill listing, then drive a real workflow against the upstream server end-to-end. Reading the SKILL.md is not a substitute for using it — selection misses, broken envelopes, and missing operating principles only show up under actual use.
5. **Update the bundled-skills list in `<repo>/README.md`** so the skill is documented alongside the existing `toolproxy-*` entries.

If the upstream's tool surface changes later (added/removed/renamed tools), the SKILL.md does not auto-update. Treat skills as code that drifts.

## See also

- **Existing examples in `<repo>/skills/`**: `toolproxy-serena-explore`, `toolproxy-serena-edit`, `toolproxy-serena-memory`, `toolproxy-serena-session`. Read at least one before authoring — these are the source-of-truth skill files (the copies under `.claude/skills/` are install artifacts).
- **agentskills.io spec**: <https://agentskills.io/specification> — formal frontmatter and directory rules.
- **ToolProxy README** (`<repo>/README.md`) and **ToolProxyMCP README** (`<repo>/ToolProxyMCP/README.md`): server-side context — only the three top-level proxy tools (`call_external_tool`, `install_skills`, `list_servers`) are visible to the host; everything upstream goes through `call_external_tool`.
