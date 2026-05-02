---
name: toolproxy-sqltools-catalog
description: Analyze and search C# data-access code with SqlTools — catalog a source tree, list existing catalogs, search data-access methods by name or database behavior, inspect a specific catalog hit, or analyze one file ad hoc. Use this when the user asks which code calls a stored procedure, where ad-hoc SQL lives, or wants to inventory database access patterns in a codebase. Prefer this over raw text search when the goal is specifically SQL and stored-procedure usage.
---

# SqlTools: data-access cataloging (via ToolProxy)

Code-to-database tracing for C# projects. These tools help answer "which methods touch the database, how do they do it, and where are the stored procedure calls or ad-hoc SQL statements?"

## Operating principles for cataloging

- **Catalog once, search many times.** `catalog_data_access_methods` builds the searchable index; `search_methods` and `get_method_by_ordinal` pay off after that.
- **Use `analyze_single_file` for spot checks, not whole-repo discovery.** It is ideal when the user already has one file in hand.
- **Treat ordinals as search-session local.** `get_method_by_ordinal` only makes sense against a specific prior `search_methods` result set.
- **Search by behavior, not just name.** `databaseType` and `usesStoredProcedure` are there because the interesting question is often "how does this code talk to the database?"
- **Use `read_file` surgically.** It is for opening a specific catalog artifact or source file after the catalog has already narrowed what matters.

## How to invoke

All calls go through the ToolProxy `call_external_tool` dispatcher. The envelope is always:

```json
{
  "server": "SqlTools",
  "tool": "<tool name>",
  "arguments": { /* tool-specific parameters */ }
}
```

Every example below repeats the full envelope so it can be copied without inferring structure from elsewhere.

## Tools

### `catalog_data_access_methods` — build a catalog from a source tree

Scans a directory of C# files and produces catalog artifacts describing data-access methods.

**Key parameters:**
- `sourceDirectory` — source tree to analyze.
- `outputDirectory` — optional destination for the catalog; omit for the default `DataAccessCatalog` under the application directory.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "catalog_data_access_methods",
  "arguments": {
    "sourceDirectory": "C:\\Development\\SomeApp\\src",
    "outputDirectory": "C:\\Development\\SomeApp\\DataAccessCatalog"
  }
}
```

### `list_catalogs` — list available catalogs

Use this before searching when you are not sure where the last catalog run wrote its output.

**Key parameter:**
- `baseDirectory` — optional directory to search for catalogs.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "list_catalogs",
  "arguments": {
    "baseDirectory": "C:\\Development\\SomeApp"
  }
}
```

### `search_methods` — search the catalog for data-access methods

The primary read tool after cataloging. Supports name, database type, and stored-procedure filters.

**Key parameters:**
- `catalogDirectory` — directory containing the catalog.
- `nameFilter` — optional partial match on method, class, or namespace names.
- `databaseType` — optional exact database type filter.
- `usesStoredProcedure` — optional true/false filter.
- `maxResults` — optional; defaults to 50.

**Example — stored-procedure callers matching a name clue:**

```json
{
  "server": "SqlTools",
  "tool": "search_methods",
  "arguments": {
    "catalogDirectory": "C:\\Development\\SomeApp\\DataAccessCatalog",
    "nameFilter": "User",
    "usesStoredProcedure": true,
    "maxResults": 20
  }
}
```

### `get_method_by_ordinal` — fetch one search hit in detail

Use this immediately after `search_methods` when the catalog results are numbered and you want the full record for one entry.

**Key parameters:**
- `catalogDirectory` — directory containing the catalog.
- `ordinal` — 1-based ordinal from the prior search result.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_method_by_ordinal",
  "arguments": {
    "catalogDirectory": "C:\\Development\\SomeApp\\DataAccessCatalog",
    "ordinal": 3
  }
}
```

### `analyze_single_file` — inspect one C# file for data-access methods

Use this for targeted analysis when cataloging the whole tree would be overkill.

**Key parameter:**
- `filePath` — full path to the C# file.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "analyze_single_file",
  "arguments": {
    "filePath": "C:\\Development\\SomeApp\\src\\Data\\UserRepository.cs"
  }
}
```

### `read_file` — read a specific catalog artifact or source file

Use this only after the catalog or single-file analysis has already narrowed what file you need to inspect.

**Key parameters:**
- `filePath` — file to read.
- `encoding` — optional; defaults to UTF-8.
- `maxLength` — optional character cap, max 50000.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "read_file",
  "arguments": {
    "filePath": "C:\\Development\\SomeApp\\DataAccessCatalog\\methods.json",
    "maxLength": 5000
  }
}
```

## Common workflows

### Finding which code calls a stored procedure

1. `catalog_data_access_methods` if no catalog exists yet.
2. `search_methods` with `usesStoredProcedure: true` and a name clue.
3. `get_method_by_ordinal` on the best hit.

### Surveying ad-hoc SQL usage in a codebase

1. `catalog_data_access_methods` on the source tree.
2. `search_methods` with `usesStoredProcedure: false`.
3. `read_file` only for the exact catalog artifact or source file you need next.

### Spot-checking one repository class

1. `analyze_single_file` on the file.
2. If that reveals a broader pattern, escalate to a full catalog run.

## See also

- **`toolproxy-sqltools-schema`** — inspect the tables and stored procedures that the code is calling.
- **`toolproxy-sqltools-query`** — verify or run read-only SQL against the live database.