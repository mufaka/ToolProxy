# Authoring skills for ToolProxy

How to add a new `toolproxy-<server>-<surface>` skill that the host (e.g. Claude Code) will load lazily after the agent runs `install_skills`.

## Where things live

- **Source of truth:** `<repo>/skills/<skill-name>/SKILL.md`. Hand-authored markdown with YAML frontmatter.
- **Naming convention:** `toolproxy-<server>-<surface>`, all lowercase, hyphenated. `<server>` matches the upstream server's role (e.g. `serena`), `<surface>` is the slice of its API the skill covers (`explore`, `edit`, `memory`, `session`). One skill per coherent surface — splitting Serena into 4 skills is intentional, not accidental.
- **Bundling:** `ToolProxyMCP/ToolProxy.csproj` has a `<Content Include="..\skills\**\*" CopyToOutputDirectory="PreserveNewest" Link="skills\%(RecursiveDir)%(Filename)%(Extension)" />` glob. Add files anywhere under `<repo>/skills/` and they ship automatically — no csproj edits.
- **Installation:** the agent calls `install_skills(skills_root)`, which copies each `<proxy-install-dir>/skills/<name>/` subtree to `<skills_root>/<name>/`. For Claude Code project-local, that's `<project_root>/.claude/skills`. After the first install in a fresh project, the user must restart the host once so live skill reload starts watching the directory.

## Frontmatter

Every `SKILL.md` starts with:

```yaml
---
name: toolproxy-<server>-<surface>
description: <see below — this is the only thing the host loads into base context>
---
```

No other fields. The host reads `description` to decide whether to lazy-load the body; the body is invisible until then.

## Writing the description (the load-bearing part)

The description is everything. It's the only content in the host's base context, and the host's tool selection is what the whole skills-based design hinges on. Spend disproportionate effort here.

Structure it roughly as three layers, in one dense paragraph:

1. **What it does** — concrete capability list, not a server pitch. "Locate classes, functions, methods... survey what's inside a file or directory... trace references and call sites — all without reading whole files."
2. **When to use it** — include the casual phrasings users actually type. "...even when they don't name any specific tool ('where is X', 'show me the Foo class', 'what uses this method', 'what's in McpManager.cs')." The host matches against these literally; without them it won't pick the skill on a casual ask.
3. **Comparison vs. host tools** — if the skill replaces or strongly supersedes a built-in (Read, Edit, the host's own memory), say so explicitly: "Strongly prefer over the host's Read tool... — dramatically more token-efficient and surfaces structure Read cannot." Without this, the host defaults to its built-ins and the skill never fires.

Negative examples are sometimes useful too — `toolproxy-serena-session` explicitly says "Most sessions never need this skill at all... Reach for it only when something is broken in a way that points to onboarding, never preemptively." That keeps the host from over-firing on a one-time setup skill.

## Body structure

The four existing Serena skills converged on this layout. Reuse it:

```
# <Server>: <surface> (via ToolProxy)

<one-paragraph intro: what this surface is, why it exists>

## Operating principles for <activity>

- <bulleted opinions: prefer-this-over-that, when-to-stop, common pitfalls>
- <include reminders that won't be obvious: "line numbers are 0-based", "don't re-read after a successful edit">
- <if the skill supersedes a host tool, repeat the prohibition here in stronger terms>

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "<ServerName>",
  "tool": "<tool name>",
  "arguments": { /* tool-specific parameters */ }
}
```

Every example below repeats the full envelope so it can be copied without inferring structure from elsewhere.

## Tools

### `tool_name` — <one-line purpose>

<short prose>

**Key parameters:**
- `param` — description.

**Example — <verb-first scenario>:**

```json
{
  "server": "<ServerName>",
  "tool": "tool_name",
  "arguments": { ... }
}
```

## Common workflows

### <Goal>

1. Step.
2. Step.

## See also

- **`toolproxy-<sibling>`** — when to reach for it instead.
```

## Conventions to match

- **Always show the full envelope in every example.** Don't abbreviate to just the `arguments` block. The body is loaded in isolation; the reader can't infer envelope shape from context.
- **Server name in examples is case-sensitive and must match `appsettings.json`** exactly (e.g. `"Serena"` capitalized, `"context7"` lowercase). The dispatcher does exact-match lookup — wrong casing returns "Server '<x>' is not configured or not running."
- **Be opinionated, terse, slightly imperative.** "Don't fetch bodies until you need them." "Locate before you edit." "Don't verify after success." Match the existing Serena skills' tone — these read more like operational runbooks than reference docs.
- **Cross-link siblings** in `## See also` so the host can chain skills (explore → edit, edit → session for failure recovery, etc.).
- **Workflows beat exhaustive parameter docs.** The agent can fall back to the live tool schema for arg details; what it can't recover is the *sequencing* ("find_referencing_symbols before rename_symbol"). Spend body length on workflows, not on duplicating JSONSchema.

## Authoring checklist

- [ ] Skill directory created at `<repo>/skills/<name>/SKILL.md`.
- [ ] Frontmatter has `name` matching the directory and a description that names casual user phrasings.
- [ ] Description explicitly compares against host built-ins if the skill should win over them.
- [ ] Every example shows the full `{server, tool, arguments}` envelope.
- [ ] Server name in examples matches `appsettings.json` casing.
- [ ] At least one `## Common workflows` recipe per non-trivial multi-step use case.
- [ ] `## See also` links to any sibling skills.
- [ ] Built once (`dotnet build` from repo root) — confirm the new file appears under `ToolProxyMCP/bin/.../net10.0/skills/<name>/SKILL.md`.
- [ ] Run `install_skills` against a test project's `.claude/skills` and confirm the directory shows up there.
- [ ] Restart the host once after first install in a fresh project.

## Why this design

The progressive-disclosure shape (descriptions in base context, bodies lazy-loaded) is the whole point of the skills-based refactor — it's what replaced the abandoned embedding/Ollama narrowing. The host agent's tool selection on description text is more reliable than cosine retrieval was, but only if the descriptions carry the casual phrasings and host-tool comparisons that drive the match. A vague description silently regresses the system to host defaults.
