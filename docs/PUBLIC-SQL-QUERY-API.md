# Public SQL query API

`POST /api/dataset/{datasetId}/query/run`

Runs one read-only SQL statement against a dataset **as a named end user**, with that user's table grants, column grants and row-level security applied. Built for Relay's dataset agent, where a language model writes the SQL.

This repo has no Swagger/OpenAPI, so this document plus the DTOs in `Application.Shared/Models/Data/PublicQueryModels.cs` are the contract.

> Note the prefix. The public API is `api/dataset` (**singular**); the internal cookie-authenticated workbench is `api/datasets` (**plural**, `QueryController`). They are different endpoints with different auth and different policies.

## Authentication

Two headers, both required:

| Header | Value |
|---|---|
| `X-Api-Key` | The API key (`fb_…`). `Authorization: Bearer <key>` is also accepted. Carries the **company**. |
| `X-User-Id` | The acting end user. `Userid` is accepted as a fallback. |

`X-User-Id` is the Entra **object id (`oid`)** — the same value the internal app sends as its `UserId` header, and what `DatasetUser.UserId` is populated with on the dataset-creation path. See `PUBLIC-API-MIGRATION-HANDOFF.md` §4.

**`X-User-Id` is caller-asserted.** Nothing here proves the human behind it: whoever holds the key can name any user in the company. Two consequences for the caller:

- Derive it **server-side** from your own authenticated principal. Never forward a browser-supplied value.
- The key must stay server-side. It is a trusted service credential, not a user token.

The API key must also be **scoped to the dataset** (an `ApiKeyScope` row with `CanRead`). That is intentionally a separate control from the user's grants: the scope is the operator's switch over the integration as a whole — pull it and the integration stops regardless of anyone's grants — and it is the only thing bounding the damage if the calling app is compromised. If the scope row narrows to specific tables (`ApiKeyScope.TableName`), the query is additionally restricted to those.

`datasetId` is compared **case-sensitively** in the scope check. Send it exactly as returned by `GET /api/dataset/user`.

## Request

```json
{
  "sql": "SELECT region, SUM(amount) AS total FROM orders GROUP BY region",
  "maxRows": 100,
  "snapshotMode": null,
  "includeRows": true
}
```

| Field | Type | Notes |
|---|---|---|
| `sql` | string | Required. One statement, `SELECT` or `WITH … SELECT`. Max 20 000 chars. |
| `maxRows` | int? | Clamped to 1…1000. Omitted → 100. The applied value comes back as `rowCap`. |
| `snapshotMode` | bool? | **Omit it.** When null it is derived exactly as the data catalog derives it (`SourceType != External`), so the schema you were shown and the schema you query are the same layer. Setting it explicitly while having read the catalog for the other layer is how you get "column not found" on a column you can plainly see. |
| `includeRows` | bool | Default true. False validates and returns the column shape without transferring rows. |

Request bodies are capped at 64 KB.

## Response

```json
{
  "sql": "SELECT region, SUM(amount) AS total FROM orders GROUP BY region",
  "effectiveSql": "WITH \"orders\" AS (SELECT \"id\",\"amount\",\"region\" FROM main.\"orders\" WHERE \"region\" IN ('EU'))\nSELECT region, SUM(amount) AS total FROM orders GROUP BY region",
  "columns": [ { "name": "region", "dataType": "VARCHAR", "isNullable": true, "isPrimaryKey": false } ],
  "rows": [ { "region": "EU", "total": 30.0 } ],
  "rowsReturned": 1,
  "truncated": false,
  "rowCap": 100,
  "elapsedMs": 12,
  "snapshotMode": true,
  "tablesReferenced": [ "orders" ],
  "security": {
    "maskedColumns": [ "orders.salary" ],
    "rowFilters": [ { "tableName": "orders", "columnName": "region", "allowedValueCount": 1 } ],
    "columnMaskingApplied": true,
    "rowSecurityApplied": true
  },
  "error": null,
  "errorCode": null
}
```

`effectiveSql` is the SQL that actually ran. It is the most useful field when driving a model: if the model writes `WHERE salary > 100` and gets "column not found", the effective SQL shows it that `salary` was never in the relation it queried.

`truncated` means exactly one thing: **at least one row existed beyond the `rowCap` rows returned**. It is not a total count, it reflects `rowCap` rather than the engine's own 5000 ceiling, and it counts *secured* rows — row-level security is applied inside the secured relation, i.e. before the cap.

`security.rowFilters` reports the value **count**, never the values.

`security.maskedColumns` names columns that exist but were excluded. That discloses their existence, consistent with `GET /api/dataset/{id}/column-access`, which already names them.

## Status codes

| Code | Meaning |
|---|---|
| `200` | Ran — **or** was rejected for a SQL/permission reason inside the dataset, with `error` + `errorCode` set and `rows` empty. |
| `400` | `sql` missing/blank, or no `X-User-Id`. |
| `401` | Invalid, missing, revoked or expired API key. |
| `403` | The **API key** is not scoped to this dataset. An operator problem: fix the key. |
| `404` | Dataset not found, another company's, or not shared with the acting user. Deliberately indistinguishable. |
| `429` | Rate or concurrency limit. |

A denial for a table or column is `200` + `errorCode`, not `403`, for two reasons: it matches how `SqlQueryResult` has always reported SQL problems on the internal endpoint, and it keeps `403` meaning exactly one thing. `table_not_permitted` (fix the share) and a missing key scope (fix the key) are different problems for different people, and collapsing both into 403 makes them indistinguishable in logs.

## Error codes

| `errorCode` | Cause |
|---|---|
| `missing_sql` | Empty `sql`. |
| `sql_too_long` | Over the character limit. |
| `not_a_select` | First keyword is not `SELECT`/`WITH`. |
| `multiple_statements` | More than one statement. |
| `forbidden_function` | A denied function/namespace, or a table-valued function where a table was expected. |
| `qualified_reference_not_allowed` | A schema-qualified reference such as `main.orders`. Use the bare name. |
| `unknown_table` | Not in the data catalog for this dataset — it may not exist, **or it may exist but have no column documentation**. |
| `table_not_permitted` | Outside the user's grants, the key's scope, or both. |
| `column_not_permitted` | A column the user cannot read, or no readable columns in a referenced table. |
| `cte_name_conflict` | A CTE named the same as a referenced table. Rename the CTE. |
| `security_not_enforceable` | Masking/RLS cannot be guaranteed — nothing was run. See below. |
| `schema_unavailable` | Tables or columns could not be read, so access could not be verified. Transient; retry. |
| `query_timeout` | Exceeded the wall-clock limit. |
| `sql_error` | The engine rejected the SQL. |
| `not_a_member` / `missing_role` | Only when `PublicApi:EnforceActingUserRoles` is on (off by default). |

## What is enforced, and how

Every referenced table is rewritten into a **secured relation** — a CTE projecting only granted columns and applying the RLS predicate at the leaf:

```sql
WITH "orders" AS (SELECT "id","amount" FROM main."orders" WHERE "region" IN ('EU'))
SELECT ...   -- your SQL, unchanged
```

Because the *relation* changes rather than the query text, `SELECT *` expands to granted columns only, a masked column fails to bind wherever it appears (projection, `WHERE`, aggregate, `ORDER BY`), and `COUNT(*)` counts filtered rows. Verified against DuckDB 1.3.

Consequences for the caller:

- **Use bare table names.** A CTE does not shadow a schema-qualified name, so `main.orders` would read straight past the secured relation. Qualified references to dataset tables are refused.
- **`SELECT *` is fine.** It expands to what you may read. You do not need to enumerate columns to stay inside your grants.
- **Do not name a CTE after a table** you also reference.
- **Only the dataset's documented tables are queryable.** The catalog advertises documented tables only, and the query allow-list is intersected with the same set, so "what you were told exists" and "what you may query" are identical. A granted-but-undocumented table returns `unknown_table`.

### Known limits, stated plainly

- **RLS records carry no table name.** A filter on column `region` applies to *every* referenced table having a `region` column, whether or not it means the same thing there — and a referenced table without that column is **not filtered at all**. In a join the second case is usually constrained through the join; in a standalone query on such a table it is not. `security.rowFilters` reports what was actually applied so you can see it. The real fix is a `table_name` column on `user_rls_filter`.
- **External datasets on the live path cannot be secured.** Live source tables are `{schema}.{name}`, which a CTE can neither be named nor shadow. Table-level grants still apply (they need no rewrite), so pass-through is allowed for an unrestricted user; a user with column or RLS restrictions gets `security_not_enforceable` with a message to retry with `"snapshotMode": true`. Fails closed rather than approximating.
- **Advisory only:** `is_pii` on catalog columns is metadata for humans, never an access control.

## Limits

| Limit | Value | Config |
|---|---|---|
| Requests per (key, acting user) | 60/min sliding | `PublicApi:RequestsPerMinute` |
| Concurrent executions per key | 4, queue 2 | `PublicApi:MaxConcurrent`, `:ConcurrencyQueueLimit` |
| Wall clock per query | 20 s | `PublicApi:SqlTimeoutSeconds` |
| Rows | 100 default / 1000 max | `PublicApi:DefaultMaxRows`, `:MaxMaxRows` |
| SQL length | 20 000 chars | `PublicApi:MaxSqlLength` |
| Request body | 64 KB | fixed |

The wall-clock limit is the only server-side bound on the external path — `DatabaseTableService.ExecuteQueryAsync` has no timeout of its own. Local DuckDB reads additionally open with `enable_external_access=false`, which refuses `read_csv`, `read_parquet`, `read_text`, `read_blob`, `glob`, `COPY TO`, `ATTACH`, `INSTALL`, `LOAD` and `http(s)` reads at the engine level.

A row cap does not bound the *source's* work: no `LIMIT` is pushed into your SQL (not dialect-safe, and it would change the meaning of a query that already has `ORDER BY`/`LIMIT`). A cartesian join still costs a full scan and will hit the timeout. Aggregate in SQL rather than pulling rows.

## Auditing

One row per call in ClickHouse `data_app_log.data_app_log` with `action = 'public.query_run'`: the key's company, the acting user, dataset, referenced tables, submitted SQL, row count, duration, status, and a `details` object carrying `apiKeyId`, `apiKeyPrefix`, `effectiveSql`, `rowCap`, `truncated`, `snapshotMode`, `maskedColumns`, `rlsColumns` and `errorCode`. The raw key is never logged. Fail-closed refusals additionally emit a debug-log warning.

## Examples

Ordinary query:

```bash
curl -sk -X POST "https://localhost:7434/api/dataset/$DS/query/run" \
  -H "X-Api-Key: $KEY" -H "X-User-Id: $USER" -H "Content-Type: application/json" \
  -d '{"sql":"SELECT region, SUM(amount) AS total FROM orders GROUP BY region","maxRows":100}'
```

Validate without fetching rows:

```bash
curl -sk -X POST "https://localhost:7434/api/dataset/$DS/query/run" \
  -H "X-Api-Key: $KEY" -H "X-User-Id: $USER" -H "Content-Type: application/json" \
  -d '{"sql":"SELECT * FROM orders","includeRows":false}'
```

Use `https://localhost:7434` in development. `http://localhost:5296` 307-redirects, and some clients drop the body across a redirect.
