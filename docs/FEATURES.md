# Status & Monitoring — Feature Roadmap

This document tracks candidate features for the status/monitoring module. The module monitors
**entities** (`MonitoredAsset`) of types Server, Report, Dataset, Database, DataPipeline, Table,
and DataJob; records per-ping `entity_status_history`; models `entity_dependency` edges between
entities; and surfaces a GitHub-style **Status Board**. Recent work added **Power BI lineage
discovery** (Dataset → Database / Table edges) and **database table discovery** (Table → Database
edges) — both of which now populate the dependency graph.

Legend: ✅ done · 🚧 in progress · ⬜ planned

---

## 1. 🚧 Dependency graph + impact analysis ("blast radius")

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

## 2. ⬜ Extend monitoring depth

- **Probe Database entities directly.** `AssetPingJob` only ICMP-pings Servers and HTTP-checks the
  rest. With the stored read-only `DatabaseConnection`, run `SELECT 1` and record real response
  time / up-down for Database entities.
- **Data freshness & SLA for Tables.** For Table entities, read `MAX(updated_at)` / `COUNT(*)`
  (read-only) and alert when a table is stale. Reuses the database connection — strong
  data-observability differentiator.
- **Richer endpoint checks.** Assert on response body/JSON and latency thresholds; monitor
  **SSL certificate / domain expiry** for URL-based entities.

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
