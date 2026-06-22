# Status & Monitoring — Feature Roadmap

This document tracks candidate features for the status/monitoring module. The module monitors
**entities** (`MonitoredAsset`) of types Server, Report, Dataset, Database, DataPipeline, Table,
and DataJob; records per-ping `entity_status_history`; models `entity_dependency` edges between
entities; and surfaces a GitHub-style **Status Board**. Recent work added **Power BI lineage
discovery** (Dataset → Database / Table edges) and **database table discovery** (Table → Database
edges) — both of which now populate the dependency graph.

Legend: ✅ done · 🚧 in progress · ⬜ planned

---

## 1. ✅ Dependency graph + impact analysis ("blast radius")

> Shipped as the **Dependencies & Impact** card on the entity detail page
> (`Application.Client/Components/DependencyImpact.razor`), built on the existing recursive
> `AssetDependencyTree` (`GET api/status/entities/{id}/dependency-tree`). Shows upstream
> ("Depends on"), downstream blast radius ("Impact if this fails" — transitive count + critical
> count), and a root-cause banner when the entity is unhealthy and so are its upstreams. No
> backend or schema change.


**Why:** Lineage discovery and DB table discovery now populate `entity_dependency` richly
(Dataset→Database, Table→Database, Dataset→Table), but the edges are only stored — not used.
Turning them into an impact view is what makes this an observability tool rather than a plain
uptime board.

**What:**
- **Dependency view** on the entity detail page: what an entity *depends on* (upstream) and what
  *depends on it* (downstream), each annotated with its current status.
- **Impact analysis / blast radius:** "If this database goes down, these N datasets/reports are
  affected" — a transitive downstream traversal.
- **Root-cause hinting:** when an entity is down, walk upstream and surface the first dependency
  that is *also* down ("Dataset failing because Database X is down").

**Fits:** read-only over `AssetDependency` + latest `AssetStatusHistory`; reuses the
status-severity logic from `StatusOverviewService`. No schema change.

---

## 2. 🚧 Extend monitoring depth

- ✅ **Probe Database entities directly.** `AssetPingJob` now builds a per-entity probe plan: for a
  Database entity with a stored read-only `DatabaseConnection` it opens the connection and runs
  `SELECT 1` (ClickHouse over HTTP `readonly=1`), recording real up/down + response time; otherwise
  it falls back to the previous URL check. Implemented via pure (no-DbContext) probe methods on
  `DatabaseTableService` so they run in the existing parallel probe phase. The scheduler now shares
  the web app's Data Protection key ring (`CredentialProtector` moved to `Application.Shared`) so it
  can decrypt the stored secrets.
- ✅ **Data freshness & SLA for Tables.** New per-Table `entity_table_check` config (freshness
  column + max-age minutes + enabled). On each run, an enabled Table entity is checked by reading
  `MAX([column])` + row count (read-only) through its **parent Database** connection (resolved via
  the Table→Database dependency); the entity goes **Degraded** when the newest row is older than the
  threshold, **Error** on query failure, else **Online**. Configurable on the Table entity detail
  page (`TableFreshness.razor`) with a "Run check now" button. Reuses the existing
  incident-on-status-change flow.
- ⬜ **Richer endpoint checks.** Assert on response body/JSON and latency thresholds; monitor
  **SSL certificate / domain expiry** for URL-based entities.

> **Actions required (user):**
> 1. Publish the new `entity_table_check` table (DACPAC project `Application.Database.Status`) to the
>    `statusapp` database.
> 2. **Share the Data Protection key ring with the scheduler.** Connection secrets are encrypted by the
>    web app's key ring (DPAPI-protected, `SetApplicationName("FlowByte.Application")`). The scheduler
>    must read the *same* keys or it cannot decrypt them (DB probes would all report offline). Set
>    `DataProtection:KeysPath` in `Application.Scheduler/appsettings.json` to the web app's existing key
>    directory (by default `Application/App_Data/keys`, or the deployment's `DataProtection:KeysPath`,
>    e.g. `C:\ProgramData\FlowByte\keys`). Do **not** change the web app's own path — that would orphan
>    all already-encrypted secrets. Both processes must run on the same machine (local-machine DPAPI).

## 3. ⬜ Alerting & communication

Models (`AlertRule`, `AlertInstance`) and email infra (`Application.Email`, `SalesSnapshotEmailJob`)
already exist; delivery on status change does not.
- Notify (Email / Teams / Slack / webhook) on status flip, Power BI refresh failure, or SLA breach.
- **Maintenance windows** to suppress alerts (surfaces as amber on the board).
- **Auto-create incidents** after N consecutive failed pings, linked to affected entities.

## 4. ⬜ Public status page + subscriptions

The board is for logged-in users. Add a public, per-company status page plus "subscribe for
updates" email on incident open/resolve. The `status_page` / `status_page_entity` tables appear
half-wired for this already.

## 5. ⬜ Analytics & AI

- **Uptime / SLA reporting:** monthly uptime %, SLA compliance, export — heavy aggregation via the
  existing DuckDB / ClickHouse services.
- **AI status assistant:** `IChatService` (Azure OpenAI) is already injected — answer "what's broken
  and why?" over status history + the dependency graph.
