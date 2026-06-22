# Issue #2470 — Wikidata SSE multi-pod observability (Grafana panel + alert)

**Status**: PROPOSED → ACTIVE on first commit landing
**Date**: 2026-06-22
**Author**: badsworm
**Type**: ops / observability follow-up to #2256 (Redis backplane shipped via PR #2469)
**Parent**: #1823 umbrella (Phase E F4 SSE stream)

---

## 1. Context

PR [#2469](https://github.com/meepleAi-app/meepleai-monorepo/pull/2469) shipped the Redis pub/sub backplane for the Wikidata cover-enrichment SSE stream (`IWikidataEnrichmentEventBroadcaster` ↔ `wikidata-enrichment:attempt-recorded` channel). The factory selects between `InMemoryWikidataEnrichmentEventBroadcaster` (default) and `RedisWikidataEnrichmentEventBroadcaster` via the env var `WIKIDATA_SSE_BACKPLANE`.

Observability for the SSE plane was explicitly out-of-scope of #2469 and tracked as #2470. Today:

- `IWikidataEnrichmentEventBroadcaster.SubscriberCount` is exposed as a diagnostic on the interface but **not surfaced as a Prometheus metric**.
- No counter for PUBLISH or message-received events.
- No alert when admin dead-letter page is open (an active SSE client expected) but local pod reports `subscribers == 0`.
- No runbook entry covering the investigation path.

Single-pod assumption (ADR DEC-3e) is still in force — `HPA=1`. The new metrics are foundational work that will pay off when DEC-3e is revisited; for now they protect a single admin-facing surface (the dead-letter visibility page) against silent SSE starvation.

## 2. Goals

- **G1**: surface SSE subscriber count, publish rate, and receive rate as Prometheus metrics, scraped via the existing `meepleai-api` job.
- **G2**: extend `wikidata-enrichment.json` dashboard with a dedicated row of SSE panels.
- **G3**: ship an alert that fires when subscribers stay at 0 for 15 minutes WHILE the admin dead-letter page is open (BE↔FE correlation).
- **G4**: document the alert's investigation path in `operations-manual.md`.

## 3. Non-goals

- Multi-pod Redis fan-out validation under HPA>1 — handled by a separate revisit of DEC-3e.
- Per-event traceability or per-game subscription topology.
- Adversarial subscriber-churn metrics (Nygard N-1 deferred — gauge + rates sufficient for V1).
- Per-backplane label split (`backplane=in-memory|redis`) — deployment knows which broadcaster is live via the env var; metric split would amplify cardinality without operational value.

## 4. Locked decisions (post spec-panel critique 2026-06-22)

### DEC-A — Alert threshold: 15min sustained
**Why**: tolerates SSE reconnect window + admin page reload without raising false positives. 5min was deemed too aggressive (deploy / browser restart), 30min too conservative (incidents masked).
**How to apply**: Prometheus `for: 15m` clause on the alert rule.

### DEC-B — Dashboard placement: extend `wikidata-enrichment.json`
**Why**: keep all Wikidata enrichment observability on one board so the on-call engineer doesn't tab-juggle. 8 → ~11 panels still well within Grafana density limits.
**How to apply**: add a new row "SSE plane" with 3 panels (subscribers, publish rate, receive rate) below the existing M9/M11/F1 panels.

### DEC-C — FE↔BE correlation: BE heartbeat endpoint
**Why**: the issue body suggests a "sentinel signal from the FE". A push-gateway approach is operationally heavy. Simpler: the admin dead-letter page POSTs a heartbeat every 30s to `POST /api/v1/admin/wikidata/enrichment/sse-clients/heartbeat`. BE keeps an in-memory TTL'd counter and exposes it as `meepleai_wikidata_sse_admin_clients_connected` (gauge). The alert then becomes `subscribers == 0 AND admin_clients_connected > 0 sustained 15m` — only triggers when an actual admin is watching.
**How to apply**: new endpoint + new gauge + FE hook in the wikidata-dead-letters page. TTL window: 90s (3 missed heartbeats → counted as disconnected).
**Why not Option C (publish_rate>0 && subscribers=0)**: would fire during background batch runs even when no admin needs the live feed. Operationally noisy.

### DEC-D — Drop `subscription_active` gauge
**Why**: per Newman S-2 — `subscribers > 0` already implies the Redis SUBSCRIBE is open (per `EnsureRedisSubscriptionAsync` lazy open / `MaybeReleaseRedisSubscriptionAsync` last-leave close). The two metrics carry the same information.
**How to apply**: stay with 3 SSE metrics (subscribers + 2 counters) + 1 admin-clients gauge.

## 5. Metric contracts

All metrics emitted by `MeepleAiMetrics.WikidataEnrichment.cs` (extension, same `Meter`). Prometheus exporter normalises dots → underscores; the documented Prometheus names appear below.

### `meepleai_wikidata_sse_subscribers` — Gauge
- **.NET API**: `ObservableGauge<int>` with callback reading `IWikidataEnrichmentEventBroadcaster.SubscriberCount`.
- **Semantics**: local pod's current SSE subscriber count.
- **Labels**: none in V1 (Prometheus `instance` label already disambiguates pods via scrape config).

### `meepleai_wikidata_sse_messages_published_total` — Counter
- **.NET API**: `Counter<long>`.
- **Increment site**: `IWikidataEnrichmentEventBroadcaster.Publish(...)` — both `InMemory` and `Redis` implementations call this on every event the M9 runner / M12 admin trigger / F2 bulk-retry publishes.
- **Semantics**: events emitted by the publisher. For the Redis backplane this equals the number of PUBLISH calls; for in-memory it equals the number of local Channel writes.

### `meepleai_wikidata_sse_messages_received_total` — Counter
- **.NET API**: `Counter<long>`.
- **Increment site**:
  - `InMemoryWikidataEnrichmentEventBroadcaster.Publish(...)` → increment by `_subscribers.Count` (the count of local channels that received the write).
  - `RedisWikidataEnrichmentEventBroadcaster.FanOutToLocalChannels(...)` → increment by `_subscribers.Count` once per Redis-incoming message.
- **Semantics**: per-pod messages handed off to a subscriber's bounded channel. With 2 pods and 1 admin client each, 1 PUBLISH → 2 `received` counts globally.

### `meepleai_wikidata_sse_admin_clients_connected` — Gauge
- **.NET API**: `ObservableGauge<int>` reading a TTL'd dictionary maintained by a new singleton `WikidataAdminClientHeartbeatTracker`.
- **Heartbeat endpoint**: `POST /api/v1/admin/wikidata/enrichment/sse-clients/heartbeat` (admin-only, group-level filter). Body: empty. Returns `204 No Content`.
- **TTL**: 90 seconds (3 × FE 30s ping). Entries older than TTL are evicted on read of `SubscriberCount` / metric callback (lazy GC, no background timer).
- **FE hook**: the admin wikidata-dead-letters page calls heartbeat on mount + every 30s thereafter; aborts on unmount.

## 6. Acceptance criteria (Gherkin)

```gherkin
Feature: Wikidata SSE observability + alert
  Surface the SSE plane in Prometheus, Grafana, and alerts so an operator
  catches silent SSE starvation while an admin is actively waiting for live
  dead-letter rows.

  Scenario: AC-1 Metrics are exposed
    Given the api is running with the monitoring profile
    When I curl http://api:8080/metrics
    Then the response body contains "meepleai_wikidata_sse_subscribers"
    And contains "meepleai_wikidata_sse_messages_published_total"
    And contains "meepleai_wikidata_sse_messages_received_total"
    And contains "meepleai_wikidata_sse_admin_clients_connected"

  Scenario: AC-2 Subscribers gauge tracks SubscriberCount
    Given the in-memory broadcaster has 0 subscribers
    When 2 SSE clients connect to /api/v1/admin/wikidata/enrichment/events
    Then "meepleai_wikidata_sse_subscribers" equals 2
    When 1 client disconnects
    Then "meepleai_wikidata_sse_subscribers" equals 1

  Scenario: AC-3 Publish counter ticks per Publish call
    Given subscribers count is 1
    When the M9 runner publishes 5 attempt-recorded events
    Then "meepleai_wikidata_sse_messages_published_total" delta equals 5

  Scenario: AC-4 Receive counter equals publishes × subscribers (in-memory)
    Given the in-memory broadcaster has 3 subscribers
    When 2 events are published
    Then "meepleai_wikidata_sse_messages_received_total" delta equals 6

  Scenario: AC-5 Admin heartbeat endpoint refreshes gauge
    Given no admin heartbeats in the last 90s
    Then "meepleai_wikidata_sse_admin_clients_connected" equals 0
    When admin A posts /sse-clients/heartbeat
    And admin B posts /sse-clients/heartbeat from a different session
    Then "meepleai_wikidata_sse_admin_clients_connected" equals 2
    When 91s pass without further heartbeats
    Then "meepleai_wikidata_sse_admin_clients_connected" equals 0

  Scenario: AC-6 Heartbeat endpoint enforces admin auth
    Given an anonymous request
    When I POST /api/v1/admin/wikidata/enrichment/sse-clients/heartbeat
    Then the response status is 401

  Scenario: AC-7 Grafana dashboard panels render
    Given the grafana container has loaded wikidata-enrichment.json
    When I open the dashboard
    Then 3 new panels exist with titles:
      | Panel 9 — SSE subscribers (per pod)        |
      | Panel 10 — SSE publish rate (5min rolling) |
      | Panel 11 — SSE receive rate (5min rolling) |

  Scenario: AC-8 Alert fires on starvation
    Given the alert rule WikidataSseSubscriberStarvation is loaded by prometheus
    And meepleai_wikidata_sse_subscribers == 0 for the last 15m
    And meepleai_wikidata_sse_admin_clients_connected > 0 for the last 15m
    Then the alert is in firing state with severity=warning

  Scenario: AC-9 Runbook is reachable from the alert
    Given the alert fires
    Then the annotations.runbook_url points to operations-manual.md section "SSE subscriber starvation"
    And the section documents 4 investigation steps
```

## 7. Out of scope

- Multi-pod scale validation under HPA>1 (DEC-3e revisit).
- Push gateway approach for FE→Prom direct.
- Per-event tracing / OpenTelemetry spans.
- `subscription_active` 0/1 sentinel gauge (DEC-D drop).
- `backplane=in-memory|redis` label split.
- Frontend visual indicator of "live stream healthy" status (separate UX task).

## 8. Risks + mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Heartbeat tracker memory leak under attack (1M fake heartbeats) | Low | Medium | TTL+lazy GC bounds dictionary size + admin-only auth filter |
| Counter overflow on long-running pod (`long.MaxValue`) | Negligible | None | `Counter<long>` — overflow only after >10^18 events |
| FE heartbeat double-fires (StrictMode dev) | Medium | None | Dedup by sessionId server-side (use existing `HttpContext.User.GetUserId()`) |
| ObservableGauge callback throws on broadcaster dispose | Low | Low | Wrap callback in try/catch returning 0 |

## 9. References

- Closed parent issue: #2256
- Shipping PR Redis backplane: #2469
- F4 in-process broadcaster PR: #2227
- ADR DEC-3e single-pod assumption: docs/superpowers/specs/2026-06-04-asse-a-semantic-alignment.md § DEC-3e (cross-ref in umbrella #1823)
- ADR DEC-3g metric scaffolding: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs`
- Existing dashboard: `infra/monitoring/grafana/dashboards/wikidata-enrichment.json`
- Existing alert pattern: `infra/prometheus/alerts/provider-quota.yml`
- Operations manual: `docs/for-developers/operations/operations-manual.md` § 14 Monitoring Stack
