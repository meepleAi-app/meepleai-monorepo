# Plan — Issue #2470 Wikidata SSE Observability (TDD)

**Spec**: [`2026-06-22-issue-2470-wikidata-sse-observability-design.md`](../specs/2026-06-22-issue-2470-wikidata-sse-observability-design.md)
**Branch**: `feature/issue-2470-wikidata-sse-observability` (parent: `main-dev`)
**Effort estimate**: ~10-14h across 4 phases.
**Workflow**: TDD red→green→refactor per task.

---

## Phase 1 — BE metric scaffolding (3 metrics + 1 gauge) — ~3h

### T1.1 — Extend `MeepleAiMetrics.WikidataEnrichment.cs` (RED → GREEN)
**File**: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs`
- Add `meepleai.wikidata.sse.messages.published.total` (`Counter<long>`).
- Add `meepleai.wikidata.sse.messages.received.total` (`Counter<long>`).
- Add backing field `_wikidataSseSubscribers` + `SetWikidataSseSubscribers(int)` + `ObservableGauge<int>` `meepleai.wikidata.sse.subscribers`.
- Add backing field `_wikidataSseAdminClients` + `SetWikidataSseAdminClientsConnected(int)` + `ObservableGauge<int>` `meepleai.wikidata.sse.admin_clients_connected`.

### T1.2 — Tests on the metric public surface
**File**: `apps/api/tests/Api.Tests/Observability/WikidataEnrichmentMetricsTests.cs`
- `SseMetrics_AreRegistered_WithExpectedNames` — extends existing test to assert the 4 new names appear on the `Meter`.
- `SetWikidataSseSubscribers_ClampsNegatives_ToZero`.
- `SetWikidataSseAdminClientsConnected_ClampsNegatives_ToZero`.
- `WikidataSseMessagesPublished_AcceptsLongIncrement`.
- `WikidataSseMessagesReceived_AcceptsLongIncrement`.

### T1.3 — Verify build + green tests
`dotnet build apps/api/src/Api` and `dotnet test --filter "FullyQualifiedName~WikidataEnrichmentMetricsTests"`.

---

## Phase 2 — Instrument broadcasters + admin heartbeat — ~5h

### T2.1 — `WikidataAdminClientHeartbeatTracker` singleton (RED → GREEN)
**Files**: new `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/WikidataAdminClientHeartbeatTracker.cs` + DI in `SharedGameCatalogServiceExtensions.cs`.
- `RecordHeartbeat(Guid userId, DateTime utcNow)` → upsert in `ConcurrentDictionary<Guid, DateTime>`.
- `GetConnectedCount(DateTime utcNow)` → lazy GC: evict entries where `utcNow - lastBeat > 90s`, return count.
- Tests in `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/WikidataAdminClientHeartbeatTrackerTests.cs`:
  - `RecordHeartbeat_NewUser_AddsEntry`.
  - `RecordHeartbeat_SameUserTwice_RefreshesTimestamp`.
  - `GetConnectedCount_EvictsExpiredEntries`.
  - `RecordHeartbeat_ConcurrentCallers_AllRecorded` (1000 parallel).

### T2.2 — Wire tracker → gauge via hosted refresh OR per-call set
**Decision (T2.2-a)**: prefer **per-callback read** — the existing `MeepleAiMetrics` pattern accepts a backing field updated by callers. Easiest path: gauge callback resolves tracker via DI and calls `GetConnectedCount(DateTime.UtcNow)`.
**Problem**: `MeepleAiMetrics` is static; cannot inject. **Fix**: register a hosted `WikidataAdminClientGaugeBinder : IHostedService` that on `StartAsync` registers the gauge callback via `MeepleAiMetrics.RegisterAdminClientsCallback(() => tracker.GetConnectedCount(DateTime.UtcNow))`. Add `RegisterAdminClientsCallback` to the metrics class (one-shot set on first call, guarded against re-register).
**Symmetric pattern for broadcaster**: register a callback `MeepleAiMetrics.RegisterSseSubscribersCallback(() => broadcaster.SubscriberCount)`.
- Tests: `Binder_StartAsync_RegistersCallback` + `Binder_Idempotent_OnSecondStart` (defensive against test host re-starts).

### T2.3 — Instrument `InMemoryWikidataEnrichmentEventBroadcaster`
- `Publish(...)`: increment `WikidataSseMessagesPublished` by 1. Increment `WikidataSseMessagesReceived` by `_subscribers.Count` (snapshot before write loop — race-tolerant, conservative count).
- Add test in `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/InMemoryWikidataEnrichmentEventBroadcasterMetricsTests.cs`:
  - `Publish_WithThreeSubscribers_IncrementsReceivedBy3`.
  - `Publish_WithZeroSubscribers_IncrementsPublishedBy1_AndReceivedBy0`.

### T2.4 — Instrument `RedisWikidataEnrichmentEventBroadcaster`
- `Publish(...)`: increment `WikidataSseMessagesPublished` by 1 (counts PUBLISH attempts; failed PUBLISH still counts — proxy for outbound activity).
- `FanOutToLocalChannels(...)`: increment `WikidataSseMessagesReceived` by `_subscribers.Count` (snapshot).
- IT test with Testcontainers Redis in `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/RedisWikidataEnrichmentEventBroadcasterMetricsIntegrationTests.cs`:
  - `Publish_Then_FanOut_IncrementsBothCounters` — single-pod scenario.

### T2.5 — Heartbeat endpoint
**File**: extend `AdminWikidataCoverEnrichmentEndpoints.cs`.
- `POST /sse-clients/heartbeat` → handler `HandleSseClientHeartbeat`:
  - Resolve `WikidataAdminClientHeartbeatTracker` via DI.
  - Call `tracker.RecordHeartbeat(context.User.GetUserId(), DateTime.UtcNow)`.
  - Return `Results.NoContent()`.
- Group-level `RequireAdminSessionFilter` already enforces auth.
- Tests in `apps/api/tests/Api.Tests/Integration/Routing/Admin/AdminWikidataCoverEnrichmentEndpointsTests.cs`:
  - `Heartbeat_Anonymous_Returns401`.
  - `Heartbeat_NonAdmin_Returns403`.
  - `Heartbeat_Admin_Returns204_AndIncrementsGauge`.

---

## Phase 3 — Grafana dashboard extension — ~1h

### T3.1 — Add 3 panels to `wikidata-enrichment.json`
**File**: `infra/monitoring/grafana/dashboards/wikidata-enrichment.json`.
- **Panel 9** — `SSE subscribers (current)`: stat with thresholds `red < 1 (when admin online)`, `green >= 1`. Expr: `meepleai_wikidata_sse_subscribers`.
- **Panel 10** — `SSE publish rate (5min rolling)`: timeseries. Expr: `rate(meepleai_wikidata_sse_messages_published_total[5m])`.
- **Panel 11** — `SSE receive rate (5min rolling)`: timeseries. Expr: `rate(meepleai_wikidata_sse_messages_received_total[5m])`.
- Bonus stat panel **Panel 12** — `Admin clients connected`: `meepleai_wikidata_sse_admin_clients_connected`.
- Update dashboard `description` + `tags` (add `issue-2470`).
- Increment `version` from 1 → 2.

### T3.2 — Verify JSON parses (no Grafana running)
- `python -m json.tool infra/monitoring/grafana/dashboards/wikidata-enrichment.json > /dev/null` from a docker exec (or `jq . file > /dev/null` if jq present).

---

## Phase 4 — Alert rule + runbook — ~1.5h

### T4.1 — New alert file `infra/prometheus/alerts/wikidata-sse.yml`
**Group**: `meepleai_wikidata_sse_alerts`.
**Rule**:
```yaml
- alert: WikidataSseSubscriberStarvation
  expr: |
    meepleai_wikidata_sse_subscribers == 0
    and
    meepleai_wikidata_sse_admin_clients_connected > 0
  for: 15m
  labels:
    severity: warning
    subsystem: wikidata-sse
  annotations:
    summary: "Wikidata SSE: admin online but no live subscribers ({{ $labels.instance }})"
    description: |
      An admin is actively watching the wikidata dead-letter page
      (admin_clients_connected={{ $value }}) but the local broadcaster
      reports 0 SSE subscribers for 15 minutes. Live row updates are
      not flowing — admin will see a stale page.

      Likely causes:
        1. Frontend EventSource connection dropped silently
        2. RedisWikidataEnrichmentEventBroadcaster SUBSCRIBE not opened
        3. Reverse proxy / ingress closing long-lived connections
    runbook_url: "https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/operations/operations-manual.md#22-wikidata-sse-subscriber-starvation"
```

### T4.2 — Mount the new rule file
**Files**:
- `infra/docker-compose.yml` (monitoring profile) — add volume mount `./prometheus/alerts/wikidata-sse.yml:/etc/prometheus/wikidata-sse.yml:ro`.
- `infra/prometheus.yml` `rule_files` — append `- '/etc/prometheus/wikidata-sse.yml'`.
- `infra/prometheus.staging.yml` — same.
- `infra/compose.staging.yml` if it overrides the volume list — sanity-check.

### T4.3 — Runbook entry
**File**: `docs/for-developers/operations/operations-manual.md`.
- New section `## 22. Wikidata SSE Subscriber Starvation` after § 21 Self-Hosted Runner Recovery.
- Subsections:
  - Alert: WikidataSseSubscriberStarvation (definition + threshold)
  - Investigation steps:
    1. Confirm the alert: query `meepleai_wikidata_sse_subscribers` + `meepleai_wikidata_sse_admin_clients_connected` in Grafana.
    2. Check api logs for `RedisWikidataEnrichmentEventBroadcaster.*UNSUBSCRIBE failed` or `dropped malformed message`.
    3. Verify reverse-proxy/ingress config (Traefik) — `X-Accel-Buffering: no` is set on the SSE endpoint, but check downstream headers.
    4. Ask the admin to hard-reload the dead-letter page. If the gauge climbs back to 1 → FE EventSource was dropped client-side (browser side); if it stays 0 → BE issue.
  - Resolution paths (3 short branches: FE drop, Redis backplane issue, ingress).

---

## Phase 5 — PR + review — ~0.5h

### T5.1 — Commit + push + open PR
- Conventional commits per phase.
- PR body: closes #2470 + acceptance checklist mirroring the 9 Gherkin scenarios.
- Base branch: `main-dev` (parent set via `git config branch.<branch>.parent`).

### T5.2 — `/code-review:code-review` against the diff
- Address any HIGH findings before requesting merge.
- Document any DEFER to follow-up issue (filed in same PR body).

---

## Verification gates (per spec § 6 AC matrix)

| AC | Verification |
|---|---|
| AC-1 metrics exposed | Unit test `WikidataEnrichmentMetricsTests` ✅ + manual `/metrics` curl |
| AC-2 subscribers gauge tracks count | Test in `InMemoryWikidataEnrichmentEventBroadcasterMetricsTests` |
| AC-3 publish counter ticks | Same |
| AC-4 receive counter equals publishes × subscribers | Same |
| AC-5 heartbeat refreshes gauge | `WikidataAdminClientHeartbeatTrackerTests` + endpoint IT |
| AC-6 endpoint auth | Endpoint IT 401 / 403 / 204 |
| AC-7 Grafana panels render | Manual `make dev` + visit dashboard |
| AC-8 alert fires | Pattern review only — alert evaluation tested in staging soak post-merge |
| AC-9 runbook reachable | Lint the markdown link from `runbook_url` annotation in IT (`grep`) |

---

## Out-of-scope follow-ups (capture in PR body)

- FE indicator in admin page (visual "live" badge when `EventSource.readyState === 1`).
- Multi-pod scale test under HPA>1 (DEC-3e revisit).
- Alert routing to actual on-call (Slack/PagerDuty integration) — alertmanager config not in this PR's scope.
