---
name: toolproxy-sqltools-schema
description: Inspect SQL Server schema and stored procedure metadata through SqlTools — list configured connections, enumerate tables and procedures, inspect table columns and keys, and fetch CREATE TABLE scripts or stored procedure definitions. Use this when the user asks what tables exist, what a stored procedure takes, how a table is shaped, or wants the authoritative database-side definition instead of guessing from application code. Prefer this before writing ad-hoc queries when the structure is still unclear.
---

# SqlTools: schema and database object inspection (via ToolProxy)

Database-shape inspection for SQL Server: connections, tables, stored procedures, and their definitions. These tools answer "what exists in the database and what does it look like?"

## Operating principles for schema inspection

- **Start by confirming the connection surface.** `list_connections` tells you which named SQL Server connections are available before you assume `default` is the right one.
- **List before drilling in.** `list_tables` and `list_stored_procedures` are the cheap discovery calls; use them before fetching details.
- **Use the lightest schema read that answers the question.** `get_table_info` and `get_stored_procedure_info` usually beat pulling full DDL.
- **Fetch definitions only when the exact database-side source matters.** `get_create_table_script`, `get_stored_procedure_body`, and `get_stored_procedure_definition` are for authoritative source, not first-pass exploration.
- **Schema and routine names can include schema prefixes.** Prefer `dbo.TableName` / `dbo.ProcName` when ambiguity is possible.

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

### `list_connections` — list configured SQL Server connections

Use this first when the user has multiple environments or did not specify which database to inspect.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "list_connections",
  "arguments": {}
}
```

### `list_tables` — enumerate user tables

Lists user-defined table names, optionally scoped to a schema.

**Key parameters:**
- `schemaName` — optional schema filter such as `dbo`.
- `connectionName` — optional named connection; defaults to `default`.

**Example — only dbo tables:**

```json
{
  "server": "SqlTools",
  "tool": "list_tables",
  "arguments": {
    "schemaName": "dbo",
    "connectionName": "default"
  }
}
```

### `get_table_info` — inspect columns, primary keys, and foreign keys

The right tool when the user asks what a table looks like structurally.

**Key parameters:**
- `tableName` — table name, optionally schema-qualified.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_table_info",
  "arguments": {
    "tableName": "dbo.Users",
    "connectionName": "default"
  }
}
```

### `get_create_table_script` — fetch the CREATE TABLE script

Use this when the exact DDL matters — migrations, auditing, or reproducing a table definition elsewhere.

**Key parameters:**
- `tableName` — table name, optionally schema-qualified.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_create_table_script",
  "arguments": {
    "tableName": "dbo.Users",
    "connectionName": "default"
  }
}
```

### `list_stored_procedures` — enumerate stored procedures

Lists procedure names, optionally scoped to one schema.

**Key parameters:**
- `schemaName` — optional schema filter.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "list_stored_procedures",
  "arguments": {
    "schemaName": "dbo",
    "connectionName": "default"
  }
}
```

### `get_stored_procedure_info` — inspect procedure parameters and metadata

Use this before reading the full body when the question is just "what does this proc take?"

**Key parameters:**
- `procedureName` — procedure name, optionally schema-qualified.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_stored_procedure_info",
  "arguments": {
    "procedureName": "dbo.GetUserById",
    "connectionName": "default"
  }
}
```

### `get_stored_procedure_body` — fetch just the T-SQL body

Use this when you want the implementation without additional wrapper metadata.

**Key parameters:**
- `procedureName` — procedure name, optionally schema-qualified.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_stored_procedure_body",
  "arguments": {
    "procedureName": "dbo.GetUserById",
    "connectionName": "default"
  }
}
```

### `get_stored_procedure_definition` — fetch the full stored procedure definition

Use this when the exact database object definition matters, including the wrapper declaration.

**Key parameters:**
- `procedureName` — procedure name, optionally schema-qualified.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "get_stored_procedure_definition",
  "arguments": {
    "procedureName": "dbo.GetUserById",
    "connectionName": "default"
  }
}
```

## Common workflows

### Understanding a table before querying it

1. `list_tables` to confirm the table name.
2. `get_table_info` to inspect columns and keys.
3. `get_create_table_script` only if you need exact DDL.

### Investigating a stored procedure contract

1. `list_stored_procedures` if the exact proc name is not known.
2. `get_stored_procedure_info` for parameters and metadata.
3. `get_stored_procedure_body` or `get_stored_procedure_definition` if you need implementation details.

### Working across multiple environments

1. `list_connections` to see the configured names.
2. Repeat the same schema call with the chosen `connectionName`.

## See also

- **`toolproxy-sqltools-query`** — validate and run read-only SELECT queries, or peek at table rows.
- **`toolproxy-sqltools-catalog`** — map C# data-access code to stored procedures and ad-hoc SQL usage.