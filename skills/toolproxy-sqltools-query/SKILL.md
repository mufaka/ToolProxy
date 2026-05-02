---
name: toolproxy-sqltools-query
description: Validate and run read-only SQL through SqlTools — check whether a query is safe SELECT-only SQL, execute bounded read queries, and quickly peek at table contents with top-row sampling. Use this when the user wants to inspect data, verify a SELECT statement, or look at a few rows from a table without writing full SQL by hand. Prefer this over ad-hoc schema inspection when the question is about data rather than structure.
---

# SqlTools: read-only querying (via ToolProxy)

Data inspection for SQL Server with read-only guardrails. These tools answer "is this query safe to run, and what data does the database currently contain?"

## Operating principles for read-only querying

- **Validate first when the query is non-trivial.** `validate_query` is cheap insurance before execution.
- **Everything here is SELECT-only.** If the task requires INSERT, UPDATE, DELETE, or DDL execution, this server is the wrong tool.
- **Use `select_top_rows` for a quick peek.** When the goal is just to inspect a table sample, it is faster and less error-prone than hand-writing SQL.
- **Keep result sets intentionally small.** `maxRows` and `rowCount` exist to bound output; set them deliberately.
- **Pair data reads with schema reads when column meaning is unclear.** If a result is confusing, switch to the schema skill rather than guessing the table shape.

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

### `validate_query` — check whether a query is a valid read-only SELECT

Use this before `execute_query` when the query has joins, filters, CTEs, or any complexity that makes safety worth confirming.

**Key parameter:**
- `query` — the SQL query to validate.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "validate_query",
  "arguments": {
    "query": "SELECT TOP 50 Id, EmailAddress FROM dbo.Users WHERE IsActive = 1 ORDER BY Id DESC"
  }
}
```

### `execute_query` — run a bounded read-only SELECT query

Executes a validated SELECT statement and returns rows. Best for custom questions that `select_top_rows` cannot express cleanly.

**Key parameters:**
- `query` — the SQL SELECT query.
- `maxRows` — optional; defaults to 100, max 1000.
- `connectionName` — optional named connection.

**Example:**

```json
{
  "server": "SqlTools",
  "tool": "execute_query",
  "arguments": {
    "query": "SELECT Id, EmailAddress, CreatedUtc FROM dbo.Users WHERE IsActive = 1 ORDER BY CreatedUtc DESC",
    "maxRows": 25,
    "connectionName": "default"
  }
}
```

### `select_top_rows` — sample rows from a table

The best first move for "show me some data from this table." Supports optional filtering and ordering without requiring a full SQL statement.

**Key parameters:**
- `tableName` — table name, optionally schema-qualified.
- `rowCount` — optional; defaults to 10, max 1000.
- `whereClause` — optional filter without the `WHERE` keyword.
- `orderBy` — optional ordering without the `ORDER BY` keywords.
- `connectionName` — optional named connection.

**Example — newest active users:**

```json
{
  "server": "SqlTools",
  "tool": "select_top_rows",
  "arguments": {
    "tableName": "dbo.Users",
    "rowCount": 20,
    "whereClause": "IsActive = 1",
    "orderBy": "CreatedUtc DESC",
    "connectionName": "default"
  }
}
```

## Common workflows

### Verifying a custom query before running it

1. `validate_query` on the proposed SQL.
2. If valid, `execute_query` with a bounded `maxRows`.
3. If the columns or joins seem wrong, switch to the schema skill.

### Quickly inspecting live data

1. `select_top_rows` on the table.
2. Add `whereClause` or `orderBy` only after the first sample shows what matters.

### Answering a focused data question

1. `validate_query` if the SQL is custom or complex.
2. `execute_query` with the tightest row bound that still answers the question.

## See also

- **`toolproxy-sqltools-schema`** — tables, stored procedures, and object definitions.
- **`toolproxy-sqltools-catalog`** — find the C# methods and stored procedures behind the data access path.