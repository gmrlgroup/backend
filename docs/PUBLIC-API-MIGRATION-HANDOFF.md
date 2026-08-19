# Handoff: Point the chat app at the new API-key public API

> Give this whole file to a fresh Claude session working **in the correct chat-app folder**.
> It is self-contained — it assumes no prior context.

## 0. Goal

The chat app currently reads datasets + data catalog from its **own internal backend**
(cookie/JWT user auth + `X-Company-Id` header). A **new external backend** now exposes the
same data behind an **API-key + `X-User-Id`** public API. We want the chat app to consume
that new API for dataset + catalog (and, where needed, feed complete dataset info to the
Python AI service `ApplicationAi`).

**Decided:** the API key **stays server-side** — the chat app's own server proxies to the new
external API (see §4). The WASM client never sees the key. The one thing still to settle is
**(B)** two data gaps in the new backend (§5). Read §4 and §5 first.

---

## 1. The new public API contract (already built in the backend)

Backend files (for reference — do **not** edit these unless §5 gaps must be closed):
- `Application/Controllers/PublicApiControllerBase.cs`
- `Application/Controllers/PublicDatasetController.cs`
- `Application/Controllers/PublicDataCatalogController.cs`
- `Application.Shared/Services/Data/PublicDatasetApiService.cs`

**Authentication / tenancy**
- Auth scheme: **API key**. Send it as `X-Api-Key: <key>` **or** `Authorization: Bearer <key>`.
- The **company/tenant is derived from the API key** (not a header anymore).
- The **acting user is sent per request** in the `X-User-Id: <userId>` header (required; 400 if missing).
- So the old `X-Company-Id` header is **not** used by these endpoints.

**Endpoints** (routes intentionally match the chat client's existing paths):

| Method & route | Returns | Notes |
|---|---|---|
| `GET /api/dataset/user` | `List<PublicDatasetDto>` | Datasets the user can access (visibility already scoped server-side). |
| `GET /api/datacatalog/{datasetId}` | `DataCatalogDto` | Tables + columns, already trimmed to the user's table/column access. |
| `GET /api/dataset/{datasetId}/table-access` | `List<UserTableAccessDto>` | Per-table grants (+ nested column grants). |
| `GET /api/dataset/{datasetId}/column-access` | `List<UserColumnAccessDto>` | Flat per-column grants. |
| `GET /api/dataset/{datasetId}/rls` | `List<UserRlsFilterDto>` | `allowedValues` is a **JSON string**, not an array. |
| `GET /api/dataset/{datasetId}/credentials` | `DatasetCredentialDto` | Server-to-server only. Full connectable shape: `name, type, host, port, databaseName, useSsl, username, password` (decrypted), `driver, filePath`. External → DB connection details; Local → `type=1 (DUCKDB)` + `filePath`. (See §5a.) |

The first two endpoints are the only ones the DatasetSwitcher itself needs. The access/RLS/creds
endpoints exist for whatever feeds the AI service.

---

## 2. What the chat app does today (files to change)

Primary consumer: **`Application.Client/Components/DatasetSwitcher.razor`**
- On init (`GetCompanyDefaultDataset`): if `sessionStorage["dataset"]`/`["datacatalog"]` are empty →
  `GET api/dataset/user`, picks `IsDefault`, then `SwitchDataset(...)`.
- `OpenDatasetSwitcher`: `GET api/dataset/user`, filters `!IsDeleted && !IsMessageDataset`.
- `SwitchDataset`: `GET api/datacatalog/{dataset.Id}`, then stores `dataset` + `datacatalog` in
  `sessionStorage` and in `StateContainer`.
- It uses the **default injected `HttpClient`** (same-origin, chat app's own backend).

Other relevant chat-side files (typed clients, same pattern — search for callers of
`api/dataset` / `api/datacatalog`):
- `Application.Client/Services/DatasetClientService.cs`
- `Application.Client/Services/DataCatalogClientService.cs`
- `Application.Client/Services/UserAccessClientService.cs`
- `Application.Client/Services/DatasetCredentialClientService.cs`
- HttpClient / header wiring in the **Client `Program.cs`** (find where `X-Company-Id` is attached —
  likely a `DelegatingHandler`).

The chat→AI integration (where a `Dataset` + `DataCatalog` are serialized and sent to
`ApplicationAi`) — locate it (e.g. `Application.Shared/Services/**` chat service, or wherever the
Python service is called) and confirm exactly which dataset fields it needs. See §5.

---

## 3. Data model shapes the chat client expects (for verifying the new DTOs)

`GetFromJsonAsync` uses web defaults (**camelCase, case-insensitive**). `[JsonPropertyName]`
attributes on the chat models override casing. Enums serialize as **numbers** (no
`JsonStringEnumConverter` is registered).

**`DatasetType` enum (chat side):** `0=MSSQL, 1=DUCKDB, 2=CLICKHOUSE, 3=POSTGRESQL, 4=MYSQL, 5=SQLITE, 6=EXCEL, 7=CSV`.
The backend maps its `DataSourceType` to this numeric value — **verify the ordering lines up**.

**Dataset JSON (from `/api/dataset/user`)** — client reads `id, name, type, description, isDefault, isMessageDataset, isDeleted`:
```json
{ "id": "ds-123", "name": "SalesDB", "type": 1, "description": "...",
  "isDefault": true, "isMessageDataset": false, "isDeleted": false, "companyId": "acme",
  "host": "", "port": 1433, "username": "", "password": "", "driver": "" }
```

**DataCatalog JSON (from `/api/datacatalog/{id}`)** — outer camelCase, **inner snake_case**:
```json
{
  "tableMetadata": [
    { "dataset_id": "ds-123", "table_name": "orders", "table_description": "", "companyId": "acme",
      "columns": [
        { "dataset_id": "ds-123", "table_name": "orders", "column_name": "amount",
          "column_description": "", "data_type": "decimal", "max_length": null,
          "is_nullable": true, "is_primary_key": false, "table_relations": [] }
      ] }
  ]
}
```
> ⚠️ Case-insensitive matching will NOT rescue `table_name` vs `tableName` (different characters).
> The catalog inner fields **must** be snake_case. Confirm the `*Dto` classes carry
> `[JsonPropertyName("table_name")]` etc. (I did not finish reading the DTO files — verify this.)

---

## 4. Security: server-side proxy (BFF) — DECIDED

The API key **must stay server-side**. `DatasetSwitcher.razor` runs in **Blazor WebAssembly**
(`InteractiveWebAssemblyRenderMode`); any HttpClient call it makes — and any key attached to it —
**runs in the browser and is fully visible to the end user**. So the key is **never** attached in
`Application.Client` (WASM).

**Approach: proxy through the chat app's own server.**
- The WASM client keeps calling the chat app's **own same-origin** `api/dataset/user` and
  `api/datacatalog/{id}` (no change to how the client makes the call).
- Add/keep thin controllers on the **chat app's server** that forward each request to the new
  external API, attaching:
  - `X-Api-Key` — read from **server** config / secret store (e.g. user-secrets, env var, key vault).
    Never in `appsettings` that ships to the client, never in `Application.Client`.
  - `X-User-Id` — the authenticated user's id taken **server-side** from
    `HttpContext.User` (`ClaimTypes.NameIdentifier`). Do **not** let the browser supply it.
- Register a typed/named `HttpClient` on the server pointing at the external API base URL.

Net effect: `DatasetSwitcher.razor` needs **little or no change**. The work is on the chat
**server** — swap those controllers' data source from the internal service to an HttpClient call
to the external API. The chat→AI path (§5) is also server-side, so it can attach the key the
same way.

Concrete checks that follow from this decision:
- API key lives only in server configuration; grep the client project to confirm it never appears there.
- `X-User-Id` is set from the server-side auth principal, never read from an incoming request header.

---

## 5. Backend gaps — now addressed (with remaining notes)

**5a. Connection details — DONE (option i).**
`DatasetCredentialDto` now carries the full connectable shape: `name, type, host, port, databaseName,
useSsl, username, password` (decrypted), `driver, filePath` (+ `apiKey`/`connectionString` = null).
`GetCredentialAsync` (`PublicDatasetApiService.cs`):
- **External** dataset → host/port/databaseName/useSsl/username/password + `type` (engine); `filePath` set
  when the external source is itself a DuckDB file.
- **Local** dataset → `type = 1 (DUCKDB)` + `filePath` = the dataset's DuckDB file (`{path}/{id}.duckdb`).
  (Previously returned null for Local.)

⇒ The chat/AI can now assemble a connection for external DBs. **Caveat:** a Local DuckDB `filePath` is only
usable by a **co-located** consumer; a remote AI service should route those queries through the backend
(option ii) rather than opening the file. That routing choice is a chat/AI-side decision — the backend now
exposes everything option (i) needs.

**5b. Catalog column flags — PARTIALLY DONE.**
`GetDataCatalogAsync` now populates per column: `data_type`, `column_description` (from saved docs),
`is_nullable` and `is_primary_key` — **real** values for Local/DuckDB tables (via `PRAGMA table_info`);
for a live **external** source the schema-only read doesn't surface nullable/PK yet, so those fall back to
`true`/`false`.
Still stubbed because this backend has **no backing store** for them: `table_description = ""`,
`table_relations = []` (no FK/relationship catalog), `max_length = null`. Populate these only if
`ApplicationAi` actually needs them — it would require first capturing table descriptions + FK relationships
(and column max-length) in the backend.

Other minor notes: `IsMessageDataset` and `IsDeleted` are always `false` from the public API
(message datasets aren't exposed — fine for the switcher, which filters them out anyway).

---

## 6. Suggested implementation order

1. §4 is **decided: server-side proxy, key stays server-side.** Still decide **§5a** (connection strategy).
2. Add config on the chat **server** for the external API base URL + API key (from a secret store,
   not client-shipped `appsettings`). Register a typed/named `HttpClient` for it.
3. Implement/point chat-**server** controllers for `GET api/dataset/user` and
   `GET api/datacatalog/{id}` to forward to the external API, attaching `X-Api-Key` (server config)
   and `X-User-Id` (from the server-side auth principal). Leave `DatasetSwitcher.razor` untouched.
4. Verify DTO JSON matches §3 (esp. snake_case catalog fields + numeric `type` enum ordering).
   Fix DTO `[JsonPropertyName]`s in the backend if needed.
5. Point the chat→AI path (also server-side; attach the key the same way) at complete dataset info
   per the §5a decision (merge credentials, or switch to backend-executed queries).
6. Test end-to-end: login → switcher lists only accessible datasets → switch → catalog loads →
   AI query runs against the right source with access/RLS respected.

## 7. Verification checklist
- [ ] `GET /api/dataset/user` returns only the user's datasets; `type` is the correct number.
- [ ] `GET /api/datacatalog/{id}` returns snake_case fields; tables/columns trimmed to access.
- [ ] API key is **never** present in any WASM/browser payload.
- [ ] `X-User-Id` is the authenticated user, not spoofable by the browser (set server-side).
- [ ] AI service can connect (or backend executes queries) per §5a.
- [ ] RLS filters (`/rls`) are applied wherever rows are returned.
