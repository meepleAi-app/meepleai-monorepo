---
title: Libro Game AI Assistant — Phase 4 Detailed Implementation Plan (Launch Preparation)
status: draft
type: implementation-plan-detail
date: 2026-05-04
parent: docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md
phase: 4
duration_weeks: 4
review-cycle: spec-panel-multi-expert
experts: Michael Nygard / Kelsey Hightower / Lisa Crispin / Karl Wiegers / Sam Newman
---

# Phase 4 — Launch Preparation (Detailed)

> **Pre-condition**: Phase 3 complete (G4 Translation, UI screens, Pricing engine, GDPR endpoints).
> **Calendar**: Weeks 19-22 of 22-25 total (4 calendar weeks, tight — see Effort Verdict §).
> **Branch base**: `feature/libro-game-mvp-phase1` (parent `main-dev`).
> **Expert panel lead**: Michael Nygard (Release It! — production failure modes).

---

## Spec-Panel Findings

### Convergent Insights (where experts agree)

1. **Chaos tests must have documented steady state before injection.** Nygard, Hightower, and Crispin all require that "system is healthy" be a concrete assertion (Prometheus metrics within band, all health checks green, zero error rate) before any chaos action begins. Running chaos against an already-degraded system produces meaningless results.

2. **DR drill (Task 4.3) is the highest-risk task in Phase 4.** Wiegers notes it is also the one most likely to be skipped when time runs short. Hightower confirms: the existing `infra/hetzner/disaster-recovery.md` is explicitly labelled "Stub artifact for Sprint 0. Full validation drill in Phase 4 Task 4.3." There is no `make dr-drill` target yet (file:line `disaster-recovery.md:34`). This must be implemented before the launch checklist (Task 4.5) can be signed off.

3. **Production deploy (Task 4.6) depends on `deploy-production.yml.disabled` being activated.** Newman and Hightower note the file `.github/workflows/deploy-production.yml.disabled` exists but is intentionally disabled. It must be reviewed, updated for gamebook services, and re-enabled. This is a first-time production deploy, not a repeat of staging — rollback strategy must be explicit before enabling.

4. **Usability testing (Task 4.2) must use the Sara persona tasks, not generic usability tasks.** Crispin emphasises test tasks must directly correspond to the defined Gherkin scenarios (G1.1 upload, G3.1 Q&A, G4.1 translation). Wiegers adds: 5 sessions is the minimum viable sample for qualitative research; recruiting must begin in Week 19 (parallel with chaos engineering) because scheduling 5 sessions takes 5-7 days minimum.

5. **The launch checklist (Task 4.5) is a sign-off gate, not a task.** All 8 items from vision §6.5 must have evidence produced by prior tasks (Tasks 4.1-4.4 + Phases 1-3). Task 4.5 is the verification that all evidence exists, not the production of that evidence. Wiegers notes two items (PR-1 legal review, PR-3 test set golden) have owners outside the engineering team and may already be complete or blocked independent of Phase 4.

### Productive Tensions (trade-offs revealed)

1. **Chaos depth (Nygard) vs. 4-week budget (Newman).** Nygard would run 12+ chaos scenarios including cascading failures (DB+LLM simultaneous), resource exhaustion (disk full, OOM), and slow degradation (P99 latency increase without errors). Newman counters that 4 chaos sub-tasks in 4 weeks with DR drill, usability, cost review, checklist, and production deploy is already maximally loaded. Resolution: Tasks 4.1a-d cover the highest-ROI scenarios for a WiFi-dependent mobile-first app. Cascading and resource-exhaustion chaos deferred to post-launch observability phase.

2. **DR drill scope: staging-only vs. production-like.** Hightower argues the drill should happen on a "production-like" environment (actual CAX31, actual Storage Box, real age-encrypted backups). Nygard agrees but notes running a DR drill that destroys the production environment is unacceptable. Resolution: DR drill runs on a dedicated staging CAX31 provisioned from scratch — NOT on production. Staging data is a recent (7-day-old) anonymised dump from production-equivalent seeds.

3. **WireMock vs. httpclient mock for OpenRouter chaos.** Crispin notes that a WireMock server at the test-environment level exercises the full HTTP client stack (IHttpClientFactory, retries, circuit breaker) whereas a NSubstitute mock in unit tests only covers the handler. Nygard agrees: WireMock is the right tool here because it validates the circuit breaker behaviour coded in `CircuitBreakerRegistrationService` and `ICircuitBreakerStateTracker` (confirmed at `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTracker.cs`). Resolution: Task 4.1b uses WireMock as a real HTTP proxy, running in Docker alongside the API in the staging environment.

4. **Observability gap: phantom Prometheus targets.** Hightower flags `infra/hetzner/disaster-recovery.md` Known Issue #4: `prometheus.yml` references `postgres-exporter:9187` and `redis-exporter:9121` but these exporters are not defined in any compose file. Task 4.4 must add these exporters before the cost telemetry dashboard review can be meaningful. This is a pre-existing drift from Sprint 0 that must be resolved in Phase 4.

5. **"Production-like" staging definition is under-specified.** Newman requires a concrete definition before chaos tests can be considered valid evidence. See Cross-Cutting Concerns section for the full definition.

### Spike findings (drift from plan v2 / Sprint 0)

| Finding | Plan v2 assumption | Reality | File:line evidence | Impact |
|---------|-------------------|---------|-------------------|--------|
| **F-1: `deploy-production.yml` is disabled** | Implied: production deploy workflow exists and can be triggered | `deploy-production.yml.disabled` — intentionally inactive | `.github/workflows/deploy-production.yml.disabled` | Task 4.6 must activate + update this workflow. |
| **F-2: No `make dr-drill` target** | DR runbook says "Run `make dr-drill` (to be created in Phase 4)" | Target does not exist in `infra/Makefile` | `disaster-recovery.md:34` | Task 4.3 must create this Make target as part of the drill automation. |
| **F-3: prometheus.yml references phantom exporters** | Prometheus would scrape postgres + redis metrics | `postgres-exporter:9187` and `redis-exporter:9121` have no corresponding compose service | `observability/prometheus.yml:10-16`, `disaster-recovery.md known issue #4` | Task 4.4 must add exporters before dashboard review. |
| **F-4: Loki schema deprecated (boltdb-shipper + v11)** | Loki observability stack works with `:latest` | `loki-config.yml` uses `boltdb-shipper` + `schema v11` (Loki 2.x only) | `disaster-recovery.md known issue #6` | Task 4.4 must either pin Loki to `2.9.10` or migrate to `tsdb`+`v13`. |
| **F-5: R2 weekly backup not implemented** | Backup automation includes weekly R2 offsite | `backup-to-r2.sh` not implemented; cron line commented out | `infra/hetzner/backup.cron:4`, `disaster-recovery.md known issue R2` | Task 4.3 DR drill discovers no offsite backup. Task 4.3 must note this as a gap; R2 implementation is a pre-launch prerequisite gate. |
| **F-6: Reverse proxy is Traefik, not Caddy** | Plan v2 Task 0.4 references `Caddyfile` and `docker-compose.production.yml` | Production stack uses Traefik (`compose.traefik.yml`). `disaster-recovery.md:9` explicitly documents this drift. | `infra/compose.traefik.yml`, `disaster-recovery.md:7-9` | Task 4.6 deploy steps must reference Traefik, not Caddy. |
| **F-7: Cost telemetry partially exists** | Task 4.4 assumes cost dashboard is new | `ILlmCostLogRepository`, `AdminBudgetService`, `LlmCostLast24h` metric, `ITokenTrackingService`, `UserTokenUsage` entity all exist | `Administration/Application/Services/AdminBudgetService.cs`, `Domain/Entities/UserTokenUsage.cs` | Task 4.4 must wire gamebook-specific cost tracking (photo processing + translation + Q&A per-session) into existing infrastructure, not build from scratch. |
| **F-8: Circuit breaker infra exists** | WireMock simulates rate limits against app | `CircuitBreakerStateTracker`, `ICircuitBreakerStateTracker`, 9 named services including "OpenRouter" | `Administration/Infrastructure/Services/CircuitBreakerRegistrationService.cs:14` | Task 4.1b WireMock must trigger circuit breaker state change and validate admin panel shows "OpenRouter: Open". |
| **F-9: `/metrics` and `/health` endpoints exist** | Observability endpoints needed for steady-state measurement | `ObservabilityServiceExtensions.cs` confirms: `/metrics` (Prometheus), `/health` (ASP.NET Core HealthChecks with Postgres + Redis + n8n + shared-catalog + configuration tags) | `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs:70,121` | Steady-state measurements in chaos tests use these real endpoints. |
| **F-10: `cost-telemetry.json` Grafana dashboard referenced but not verified** | Plan v2 Task 0.5 says "Create `infra/observability/grafana-dashboards/cost-telemetry.json`" | Only `api-performance.json` exists in `infra/observability/grafana-dashboards/` | `infra/observability/grafana-dashboards/` (only api-performance.json) | Task 4.4 must create or verify existence of `cost-telemetry.json`. |

### Risk register additions

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| **R-22** | Chaos test causes data corruption in staging PostgreSQL | Low | High | DR drill staging uses a separate isolated PostgreSQL instance seeded from anonymised dump. Production is never targeted by chaos tests. |
| **R-23** | DR drill discovers Storage Box backup is corrupted or missing | Medium | Critical | Run `backup.sh` manually on the night before the drill and verify the output archive is readable before the drill begins. |
| **R-24** | Usability testers reveal a blocker UX issue (e.g., photo upload flow unusable on Android) | Medium | High | Run usability sessions in Week 19-20, giving at least 1 week for hotfixes before launch checklist sign-off in Week 21. |
| **R-25** | Production deploy causes EF Core migration failure on production DB | Medium | High | Run migrations in dry-run mode (`dotnet ef migrations script`) against a production-schema clone before enabling `deploy-production.yml`. |
| **R-26** | LLM cost surge from early users exceeds $200/day hard limit | Medium | Medium | `OPENROUTER_DAILY_COST_HARD_LIMIT_USD=200` must be validated as enforced (not just logged) in Task 4.4 alerting review. |
| **R-27** | PR-1 legal review not complete by launch checklist gate | Medium | Critical (launch blocker) | Legal review owner (Aaron) must confirm status by Week 19 Day 1. If not complete, Task 4.5 and 4.6 are blocked. |
| **R-28** | `deploy-production.yml.disabled` re-activation introduces unreviewed deploy steps | Low | High | Run deploy workflow against a production-mirror environment before enabling for real production. |
| **R-29** | Loki 3.x schema incompatibility causes log ingestion failure post-launch | Medium | Medium | Pin Loki to `2.9.10` in `compose.observability.yml` as part of Task 4.4. |

---

## Task 4.1a — Production-like staging environment

**Goal**: Establish a validated "production-like" environment that is the mandatory pre-condition for Tasks 4.1b, 4.1c, 4.1d, and 4.3.

**Owner**: DevOps lead
**Effort**: 2 days
**Dependency**: Phase 3 complete, CAX31 staging server available

**Production-like definition (normative)**:

| Dimension | Production-like definition |
|-----------|---------------------------|
| Server | Dedicated Hetzner CAX31 (not dev machine), ARM64, Ubuntu 24.04 |
| Compose stack | `compose.prod.yml` + `compose.traefik.yml` + `observability/compose.observability.yml` |
| Data volume | PostgreSQL seeded with ≥ 5 users, ≥ 2 gamebooks each with ≥ 20 photos indexed |
| Secrets | Real age-encrypted secrets (not placeholder values); same format as production |
| Backup | ≥ 1 daily backup on Storage Box created by `backup.sh` and verified readable |
| Networking | Public IP + Cloudflare DNS pointing to staging domain (`staging.meepleai.com`) |
| Observability | Prometheus + Grafana + Loki running; `/metrics` scraping at 15s; all health checks green |
| Traffic shape | Simulated load: 10-20 concurrent requests via `k6` script at startup |

**Files to create/modify**:
- Create: `infra/hetzner/staging-provision.sh` (clone of `cax31-bootstrap.sh` with staging-specific env)
- Create: `infra/scripts/k6-steadystate-load.js` (k6 load script simulating photo upload + Q&A + translation)
- Modify: `infra/observability/compose.observability.yml` — add `postgres-exporter` + `redis-exporter` + `node-exporter` services (resolves F-3/Known Issue #4); pin Loki to `2.9.10` (resolves F-4/Known Issue #6); mount named volume for Promtail positions (resolves Known Issue Promtail positions)

**Implementation steps**:

- [ ] **Step 1**: Provision staging CAX31 via `hcloud server create --type cax31 --image ubuntu-24.04 --location fsn1 --name meepleai-staging --ssh-key <key>`
- [ ] **Step 2**: Run `infra/hetzner/staging-provision.sh` (bootstrap Docker + Compose + age + Storage Box mount)
- [ ] **Step 3**: Deploy stack: `docker compose -f compose.prod.yml -f compose.traefik.yml -f observability/compose.observability.yml up -d`
- [ ] **Step 4**: Verify all services healthy: `curl https://staging.meepleai.com/health` → all checks green; Grafana dashboard loading at `grafana.staging.meepleai.com`
- [ ] **Step 5**: Add missing exporters to `compose.observability.yml`:
  ```yaml
  postgres-exporter:
    image: prometheuscommunity/postgres-exporter:latest
    platform: linux/arm64
    environment:
      DATA_SOURCE_NAME: "postgresql://meeple:<password>@postgres:5432/meepleai_prod?sslmode=disable"
    ports:
      - "127.0.0.1:9187:9187"
    mem_limit: 64m

  redis-exporter:
    image: oliver006/redis_exporter:latest
    platform: linux/arm64
    environment:
      REDIS_ADDR: "redis://redis:6379"
    ports:
      - "127.0.0.1:9121:9121"
    mem_limit: 64m

  node-exporter:
    image: prom/node-exporter:latest
    platform: linux/arm64
    ports:
      - "127.0.0.1:9100:9100"
    pid: host
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
    mem_limit: 64m
  ```
- [ ] **Step 6**: Pin Loki version in `compose.observability.yml`: `image: grafana/loki:2.9.10`
- [ ] **Step 7**: Mount named volume for Promtail positions: add `promtail_positions:/tmp` volume binding
- [ ] **Step 8**: Run k6 steady-state load for 5 minutes; verify Prometheus shows `http_requests_total` increasing + no error rate > 1%
- [ ] **Step 9**: Document steady-state baseline metrics snapshot:
  - CPU utilisation: `node_cpu_seconds_total` — baseline %
  - Memory: `process_working_set_bytes{job="meepleai-api"}` — baseline MB
  - P99 latency: `http_request_duration_seconds{quantile="0.99"}` — baseline ms
  - Error rate: `rate(http_requests_total{status=~"5.."}[5m])` — baseline (should be 0)
  - Health: all checks in `GET /health` — baseline (should be all Healthy)

**Acceptance criteria**:
- [ ] All compose services are running with `docker compose ps` showing "healthy" or "running"
- [ ] `curl https://staging.meepleai.com/health` returns `200 OK` with all checks Healthy
- [ ] `curl https://api.staging.meepleai.com/metrics` returns Prometheus text format (non-empty)
- [ ] Grafana at `https://grafana.staging.meepleai.com` shows data in dashboards
- [ ] postgres-exporter at `:9187/metrics` shows PostgreSQL metrics (not phantom target)
- [ ] redis-exporter at `:9121/metrics` shows Redis metrics
- [ ] Steady-state baseline metrics documented in `docs/libro-game-assistant/chaos-steady-state-baseline.md`

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: Staging environment is healthy and production-like
  Given a Hetzner CAX31 staging server is provisioned
  And the full compose stack is deployed
  When I run "curl https://staging.meepleai.com/health"
  Then I receive 200 OK
  And all HealthChecks return Healthy (postgres, redis, shared-catalog-fts, configuration)
  And Prometheus scrapes succeed for all 4 jobs (meepleai-api, postgres, redis, node)
  And Grafana dashboard shows non-empty metric panels

# Edge: exporters missing
Scenario: Adding missing prometheus exporters resolves phantom targets
  Given compose.observability.yml references postgres-exporter:9187
  But no postgres-exporter service exists in any compose file
  When I add the postgres-exporter service definition
  And redeploy the observability stack
  Then "curl localhost:9187/metrics" returns PostgreSQL metrics

# Error: Loki schema mismatch
Scenario: Pinning Loki version prevents schema incompatibility
  Given loki-config.yml uses schema v11 and boltdb-shipper
  And Loki latest image is 3.x
  When I run "docker compose up loki"
  Then Loki logs "cannot find schema entry for period..." (3.x schema error)
  And when I pin "image: grafana/loki:2.9.10"
  Then Loki starts cleanly with schema v11
```

---

## Task 4.1b — WireMock OpenRouter rate limit simulation

**Goal**: Validate that the application's circuit breaker and retry logic handle OpenRouter 429 rate-limit responses gracefully — user sees a safe error, not a 500 crash, and circuit breaker enters Open state.

**Owner**: Backend engineer
**Effort**: 1.5 days
**Dependency**: Task 4.1a (production-like staging environment up)

**Hypothesis**: When OpenRouter returns HTTP 429 for all requests for a sustained period, the circuit breaker for "OpenRouter" (`CircuitBreakerRegistrationService.cs:14`) transitions to Open state within 3 retries, and all subsequent requests to translation/Q&A endpoints return a graceful 503 with user-readable message — not a 500 Internal Server Error. After the WireMock simulates recovery, the circuit breaker transitions to Half-Open then Closed.

**Steady state metrics (required before injection)**:
- All health checks green (from Task 4.1a baseline)
- `GET /api/v1/ask-question` with valid request → `200 OK` (P50 < 2s)
- `GET /api/v1/translate-paragraph` with valid request → `200 OK`
- Grafana: circuit breaker panel shows "OpenRouter: Closed"
- Error rate: `rate(http_requests_total{status=~"5.."}[1m])` = 0

**Failure injection mechanism**:
WireMock Docker container deployed alongside the API in staging, acting as HTTP proxy between the API and OpenRouter. WireMock stub: all requests to `api.openrouter.ai/*` return `HTTP 429 Too Many Requests` with `Retry-After: 30` header.

```yaml
# infra/chaos/wiremock-openrouter-429.json
{
  "request": {
    "method": "ANY",
    "urlPattern": ".*"
  },
  "response": {
    "status": 429,
    "headers": {
      "Content-Type": "application/json",
      "Retry-After": "30"
    },
    "body": "{\"error\":{\"message\":\"Rate limit exceeded\",\"code\":429}}"
  }
}
```

**Files to create**:
- Create: `infra/chaos/wiremock-openrouter-429.json`
- Create: `infra/chaos/compose.wiremock.yml`
- Create: `infra/chaos/run-chaos-4-1b.sh` (automates start, inject, observe, cleanup)
- Create: `docs/libro-game-assistant/chaos-4-1b-results.md` (findings template)

**Implementation steps**:

- [ ] **Step 1**: Create WireMock compose file:
  ```yaml
  # infra/chaos/compose.wiremock.yml
  services:
    wiremock:
      image: wiremock/wiremock:latest
      platform: linux/arm64
      ports:
        - "127.0.0.1:8089:8080"
      volumes:
        - ./wiremock-openrouter-429.json:/home/wiremock/mappings/openrouter-429.json:ro
      command: ["--port", "8080", "--verbose"]
  ```
- [ ] **Step 2**: Configure staging API to route OpenRouter calls through WireMock proxy: set `OPENROUTER_BASE_URL=http://wiremock:8089/api/v1` in staging compose override
- [ ] **Step 3**: Record steady state (5 min) — save to `chaos-4-1b-results.md`
- [ ] **Step 4**: Start WireMock: `docker compose -f compose.wiremock.yml up -d`
- [ ] **Step 5**: Run 10 consecutive translation and Q&A requests against staging API
- [ ] **Step 6**: Observe: circuit breaker transitions to Open state
- [ ] **Step 7**: Assert admin panel: `GET /api/v1/admin/circuit-breakers` shows `OpenRouter: Open`
- [ ] **Step 8**: Assert API responses: all translation/Q&A requests return `503 Service Unavailable` with `{"error": "AI service temporarily unavailable, please try again in 30 seconds"}` (not 500)
- [ ] **Step 9**: Stop WireMock: `docker compose -f compose.wiremock.yml down`
- [ ] **Step 10**: Observe recovery: after `RetryAfter` window, circuit breaker transitions to Half-Open → Closed within 60s
- [ ] **Step 11**: Assert final: `GET /api/v1/ask-question` returns `200 OK`
- [ ] **Step 12**: Document results in `chaos-4-1b-results.md`

**Acceptance criteria**:
- [ ] Circuit breaker for "OpenRouter" enters Open state after 3+ consecutive 429 responses
- [ ] All API endpoints return `503` (not `500`) while circuit breaker is Open
- [ ] Error message is user-readable (not internal stack trace)
- [ ] Circuit breaker recovers to Closed state within 90 seconds of WireMock removal
- [ ] Prometheus metric `circuit_breaker_state{service="OpenRouter"}` reflects state changes

**Rollback procedure**: Stop `compose.wiremock.yml`, remove `OPENROUTER_BASE_URL` override, restart API container. Circuit breaker resets on restart.

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: Circuit breaker opens on sustained OpenRouter 429s
  Given the staging environment is at steady state (circuit breaker Closed)
  When WireMock intercepts all OpenRouter requests and returns 429
  And I send 5 translation requests
  Then the circuit breaker for "OpenRouter" transitions to Open
  And all subsequent requests return HTTP 503 with user-readable message
  And no request returns HTTP 500

# Edge: partial 429s (flapping)
Scenario: Circuit breaker does not open on single isolated 429
  Given the circuit breaker threshold is 3 consecutive failures
  When WireMock returns 429 for 2 requests then 200 for 1 request
  Then the circuit breaker remains Closed
  And the retry logic handles the 429s transparently

# Error: circuit breaker state not exposed to admin
Scenario: Circuit breaker state visible in admin API
  Given the circuit breaker for "OpenRouter" is Open
  When an admin calls GET /api/v1/admin/circuit-breakers
  Then the response includes {"service":"OpenRouter","state":"Open","lastTransitionAt":"..."}
```

---

## Task 4.1c — Network namespace WiFi loss simulation

**Goal**: Validate that the frontend application (Next.js) and API handle network loss gracefully — in-flight operations complete or fail with user-readable errors, cache serves stale content where available, and the app recovers automatically when connectivity returns.

**Owner**: Frontend engineer + DevOps
**Effort**: 1.5 days
**Dependency**: Task 4.1a (staging environment up), Task 3.5b frontend components deployed to staging

**Hypothesis**: When network connectivity between the user's browser and the API is lost for 60 seconds, the frontend:
1. Shows a "Connessione persa" indicator within 5 seconds of loss
2. Serves cached translation/Q&A responses from the 24h local cache where available
3. Does not crash or show unhandled errors for in-flight requests
4. Automatically resumes normal operation within 10 seconds of connectivity restoration

**Steady state metrics**:
- Staging app accessible from test device at `https://staging.meepleai.com`
- At least 3 translations and 3 Q&A responses cached in browser (localStorage/service worker)
- No network errors in browser console

**Failure injection mechanism** (Linux network namespace — NOT production):
```bash
# On staging server: simulate outbound network loss from API perspective
# (simulates upstream LLM unreachable — equivalent to WiFi loss for AI calls)
sudo iptables -I OUTPUT -d api.openrouter.ai -j DROP
sleep 60
sudo iptables -D OUTPUT -d api.openrouter.ai -j DROP
```

For frontend WiFi simulation (testing from browser):
- Chrome DevTools Network throttle → "Offline" mode
- Or: `tc qdisc add dev eth0 root netem loss 100%` on test machine

**Files to create**:
- Create: `infra/chaos/run-chaos-4-1c.sh` (iptables injection + timer + cleanup)
- Create: `docs/libro-game-assistant/chaos-4-1c-results.md`

**Implementation steps**:

- [ ] **Step 1**: Open staging app in Chrome with DevTools console open
- [ ] **Step 2**: Pre-warm cache: translate 3 paragraphs + ask 3 Q&A questions
- [ ] **Step 3**: Record steady state: all cache keys present in localStorage, no console errors
- [ ] **Step 4**: Inject network loss: Chrome DevTools → Network → Offline (browser-side WiFi simulation)
- [ ] **Step 5**: Attempt to translate a paragraph not in cache → assert: user sees "Connessione non disponibile. Connettiti a internet per usare questa funzione." (G4.7 scenario)
- [ ] **Step 6**: Attempt to ask a Q&A question → assert: same graceful offline message
- [ ] **Step 7**: Navigate to a previously-translated paragraph → assert: cached translation is served (not network error)
- [ ] **Step 8**: Restore network: Chrome DevTools → Online
- [ ] **Step 9**: Assert: next translation/Q&A request succeeds within 10 seconds of restoration (no manual refresh required)
- [ ] **Step 10**: Verify: no unhandled promise rejections in console during entire test
- [ ] **Step 11**: Repeat steps 4-10 for API-side injection (iptables on staging server)
- [ ] **Step 12**: Document results in `chaos-4-1c-results.md`

**Acceptance criteria**:
- [ ] Graceful degradation message shown within 5 seconds of network loss (G4.7 scenario from vision §3)
- [ ] Cached responses (24h cache) served correctly when network is unavailable
- [ ] No unhandled JavaScript errors thrown during network loss
- [ ] App recovers to normal operation within 10 seconds of network restoration without manual refresh
- [ ] No data loss (in-progress photo uploads resume or fail cleanly with retry option)

**Rollback procedure**: `sudo iptables -F OUTPUT` on staging server. Chrome: Network → Online.

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: App recovers automatically after WiFi restored
  Given Sara has active session with 3 cached translations
  When WiFi is lost for 60 seconds
  Then the app shows "Connessione non disponibile" within 5 seconds
  And cached translations are accessible without error
  When WiFi is restored
  Then the app resumes normal operation within 10 seconds
  And no manual page refresh is required

# Edge: in-flight upload during WiFi loss
Scenario: Photo upload in progress when WiFi is lost
  Given Sara has uploaded 15 of 20 photos
  When WiFi is lost after photo 15
  Then the upload pauses and shows "In attesa di connessione..."
  And already-uploaded photos are not re-uploaded when connectivity restores
  And upload resumes from photo 16

# Error: no cache available during network loss
Scenario: New session with no cache during WiFi loss
  Given Sara opens a fresh session with no cached data
  When WiFi is immediately lost
  Then the app shows "Nessuna connessione. Connettiti per iniziare."
  And does not attempt infinite retries
  And does not display a white screen or JavaScript error
```

---

## Task 4.1d — PostgreSQL failover drill

**Goal**: Validate that the application handles a PostgreSQL failure (container restart / crash) gracefully — requests in-flight during the failure return 503, and after PG restart, the application reconnects automatically within 30 seconds without requiring restart.

**Owner**: Backend engineer + DevOps
**Effort**: 1 day
**Dependency**: Task 4.1a (staging environment up)

**Hypothesis**: When the PostgreSQL container is stopped and restarted (simulating a crash + OS-level recovery), the ASP.NET Core health check transitions to Unhealthy within 10 seconds, all DB-dependent endpoints return 503, and after PostgreSQL restart, the health check returns Healthy within 30 seconds without API container restart.

> Note: The vision §4.11 mentions "Region down → Hot standby Helsinki" (RTO 30 min) but this requires a second CAX31. That scenario is explicitly deferred to post-MVP. This drill covers the single-server PostgreSQL crash-and-recovery scenario (RTO target: automated recovery within 30 seconds of PG restart, not a full server provisioning drill).

**Steady state metrics**:
- `GET /health` → `{"status":"Healthy","checks":{"postgres":"Healthy","redis":"Healthy",...}}`
- All gamebook endpoints return 200 for test requests
- Prometheus: `meepleai_health_check_status{check="postgres"}` = 1

**Failure injection mechanism**:
```bash
# On staging server
docker compose stop postgres
sleep 30  # Observe degraded state
docker compose start postgres
sleep 30  # Observe recovery
```

**Files to create**:
- Create: `infra/chaos/run-chaos-4-1d.sh`
- Create: `docs/libro-game-assistant/chaos-4-1d-results.md`

**Implementation steps**:

- [ ] **Step 1**: Record steady state (health check green, sample request 200)
- [ ] **Step 2**: Stop PostgreSQL: `docker compose stop postgres`
- [ ] **Step 3**: Within 10 seconds: assert `GET /health` returns Unhealthy for postgres check
- [ ] **Step 4**: Within 15 seconds: assert `GET /api/v1/ask-question` returns 503 (not 500, not hang)
- [ ] **Step 5**: Assert Prometheus: `meepleai_health_check_status{check="postgres"}` = 0
- [ ] **Step 6**: Restart PostgreSQL: `docker compose start postgres`
- [ ] **Step 7**: Wait for PG to accept connections (healthcheck: `pg_isready`)
- [ ] **Step 8**: Assert: `GET /health` returns postgres=Healthy within 30 seconds of PG start
- [ ] **Step 9**: Assert: `GET /api/v1/ask-question` returns 200 within 35 seconds of PG start
- [ ] **Step 10**: Assert: no data was lost — verify row counts match pre-crash state
- [ ] **Step 11**: Document results in `chaos-4-1d-results.md`

**Acceptance criteria**:
- [ ] Health check transitions to Unhealthy within 10 seconds of PG stop
- [ ] DB-dependent endpoints return `503` (not `500`) during PG outage
- [ ] Application reconnects automatically within 30 seconds of PG restart (no API container restart required)
- [ ] Zero data loss confirmed (row counts identical pre/post crash)
- [ ] Prometheus metric reflects health transitions

**Rollback procedure**: `docker compose start postgres`. If PG volume corrupted: restore from daily backup per DR runbook Scenario 3.

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: API reconnects to PostgreSQL without restart
  Given PostgreSQL is running and health check is Healthy
  When I run "docker compose stop postgres"
  Then the health check transitions to Unhealthy within 10 seconds
  And all database-dependent endpoints return HTTP 503
  When I run "docker compose start postgres"
  Then the health check transitions back to Healthy within 30 seconds
  And endpoints return 200 without restarting the API container

# Edge: requests in-flight during crash
Scenario: In-flight requests during PostgreSQL crash fail gracefully
  Given a photo batch indexing job is running (background Quartz job)
  When PostgreSQL crashes mid-job
  Then the job marks itself as Failed (not hanging in Pending)
  And can be retried after PostgreSQL recovers

# Error: PostgreSQL data volume corruption
Scenario: PostgreSQL fails to start after simulated corruption
  Given the PostgreSQL container has been stopped
  And the data volume has been deliberately corrupted
  When I attempt "docker compose start postgres"
  Then PostgreSQL fails to start
  And the DR runbook Scenario 3 (data corruption) applies
  And the backup restoration procedure is initiated from last daily backup
```

---

## Task 4.2 — Usability testing 5 sessions

**Goal**: Validate the product with 5 real sessions from casual Italian boardgamers matching the Sara persona, using gamebook manuali in EN. Incorporate findings before launch checklist sign-off.

**Owner**: UX researcher (part-time, contracted)
**Effort**: 2 weeks calendar (1 week recruiting + scheduling, 1 week sessions + analysis)
**Budget**: $250 USD incentives ($50/session × 5)
**Dependency**: Phase 3 UI screens complete and deployed to staging
**Parallelism**: Can run in parallel with Task 4.1 chaos engineering (different team)

### Recruitment criteria

| Criterion | Value |
|-----------|-------|
| Age | 25-45 |
| Board game frequency | 1-3 games/month |
| Language | Italian native speaker (madrelingua) |
| English proficiency | Basic (can't read game rules fluently) |
| Device | Smartphone (Android or iOS) |
| Access to gamebook | Has at least 1 EN-language gamebook/libro game at home, OR willing to test with supplied demo gamebook |
| Exclude | BGG power-users (>50 reviews), professional translators, game designers |

**Recruitment channels**:
- Italian board game Facebook groups (La Tana dei Goblin, BoardGameGeek Italia)
- Discord server: Giochi da Tavolo IT
- Local game stores in Milan/Turin/Rome (leaflet + QR code)
- Team's personal networks

### Test scenarios (mapped to Gherkin + vision §3)

Each session runs 75-90 minutes. Observer takes notes but does not guide unless stuck > 5 minutes.

| # | Scenario | Task instruction | Corresponds to | Success criterion |
|---|----------|----------------|----------------|-------------------|
| T1 | First-time setup + photo upload | "Hai un manuale di gioco in inglese. Fotografa le prime 10 pagine e indicizzale." | G1.1, G1.2 | User completes upload without abandoning; confidence badges visible |
| T2 | Q&A durante partita | "Stai giocando e non capisci una regola. Chiedi all'app: 'quanti dadi si tirano per attaccare con una spada?'" | G3.1 | Answer appears in < 5s; user finds citation useful or useless (noted) |
| T3 | Traduzione paragrafo | "Leggi il paragrafo §15 del manuale in italiano per i tuoi amici." | G4.1 | Translation quality rated by user (1-5 Likert); no hallucination noted |
| T4 | Connessione persa | [Moderator silently disables WiFi] "Prova a fare un'altra traduzione." | G4.7 | Graceful message shown; user not confused/alarmed |
| T5 | House rule | "Il gruppo ha deciso che i critici fanno doppio danno. Salvalo nell'app." | G3.4 | User locates and uses house rule flow; rule saved |

### Success metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Task completion rate | ≥ 70% across all 5 tasks × 5 sessions | Observer checklist per task |
| Time-on-task T1 (upload) | ≤ 8 min for 10 pages | Stopwatch |
| Translation quality (T3) | Avg Likert ≥ 3.5/5 | Post-task survey |
| Critical errors (blocking) | 0 per session | Observer log |
| Non-critical issues | < 3 per session (avg) | Observer log |
| Post-session NPS | ≥ +20 | Post-session survey |

### Recording consent + data handling

- Signed consent form before session (GDPR Art. 6(1)(a) explicit consent)
- Screen recording only (no webcam unless participant opts in)
- Recordings stored in encrypted folder (age-encrypted), deleted after report publication
- Participants identified by code (P1-P5), not name, in report
- Participants informed: recordings not used for AI training

### Debrief script (post-session, 10 min)

1. "Nel complesso, quanto è stata facile da usare l'app?" (1-5)
2. "Quale parte ti ha sorpreso in positivo?"
3. "Quale parte ti ha frustrato di più?"
4. "Useresti questa app la prossima volta che giochi a un libro game in inglese?" (Likert 1-5)
5. "Hai qualcosa da aggiungere che non ho chiesto?"
6. NPS: "Con quale probabilità consiglieresti questa app a un amico?" (0-10)

### Issue severity triage

After each session, UX researcher categorises found issues:

| Severity | Definition | SLA before launch |
|----------|------------|------------------|
| P0 — Blocker | Task cannot be completed at all | Fix before Task 4.5 sign-off |
| P1 — Critical | Task completed with major struggle (>5 min retry) | Fix before Task 4.5 sign-off |
| P2 — Important | Task completed with moderate difficulty | Fix if time allows, else post-launch |
| P3 — Minor | Cosmetic or micro-friction | Post-launch backlog |

**Files to create**:
- Create: `docs/libro-game-assistant/usability-test-protocol.md` (recruitment brief, scenarios, consent form, debrief script)
- Create: `docs/libro-game-assistant/usability-results-summary.md` (template; filled after sessions)

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: Participant completes all 5 tasks without moderator intervention
  Given a participant matching Sara persona is recruited
  And staging app is available on their device
  When the participant executes T1-T5 in sequence
  Then task completion rate is 5/5
  And no blocking errors occur
  And NPS score is >= 7

# Edge: participant stuck on T1 photo upload
Scenario: Moderator intervenes after 5-minute timeout
  Given participant cannot locate the photo upload button
  And 5 minutes have elapsed
  When the moderator says "prova a esplorare il menu principale"
  Then the intervention is logged as a P1 usability issue
  And the session continues from T2

# Error: language barrier causes task failure
Scenario: English technical terms in UI confuse Italian participant
  Given UI contains an untranslated English button label
  When participant attempts T2 (Q&A)
  Then task fails due to incomprehensible UI label
  And issue is logged as P0 — localization blocker
  And it must be fixed before Task 4.5 sign-off
```

**Acceptance criteria**:
- [ ] 5 sessions completed
- [ ] All P0/P1 issues from sessions logged in GitHub issues with "launch-blocker" label
- [ ] P0/P1 issues fixed (or scope-reduced with product owner approval) before Task 4.5
- [ ] `docs/libro-game-assistant/usability-results-summary.md` published with task completion rates + NPS
- [ ] Vision §6.5 item #7 ("5 sessioni usability testing completate, feedback incorporato") can be checked off

---

## Task 4.3 — DR drill (RTO < 2h)

**Goal**: Execute a full Disaster Recovery drill for Scenario 1 (CAX31 down/lost) on the staging environment, validate the full restoration procedure from Storage Box backup, confirm RTO < 2 hours, and produce a post-drill report.

**Owner**: DevOps lead + Backend engineer
**Effort**: 1.5 days (drill day + report)
**Dependency**: Task 4.1a (staging environment with verified backups)
**Critical dependency**: At least 1 backup from `infra/hetzner/backup.sh` must exist on Storage Box, AND the backup must be readable (decrypt test required as pre-condition)

### Pre-conditions (verify the night before drill)

- [ ] `ls /mnt/storagebox/backups/postgres/` shows at least 1 `.sql.gz.age` file from the last 7 days
- [ ] Decrypt test: `age -d -i /etc/age.key /mnt/storagebox/backups/postgres/<latest>.sql.gz.age | gunzip | head -c 100` — succeeds and shows SQL content
- [ ] R2 backup status: document in pre-drill report as "not yet implemented" (F-5 known gap)
- [ ] Staging DNS record for `staging.meepleai.com` is documented and changeable via Cloudflare API
- [ ] SSH access to Hetzner Cloud API (`hcloud` CLI authenticated)

### `make dr-drill` target (to create)

```makefile
# infra/Makefile addition
.PHONY: dr-drill dr-drill-validate dr-drill-restore dr-drill-report

dr-drill: dr-drill-validate dr-drill-restore dr-drill-report

dr-drill-validate:
	@echo "==> Pre-drill validation"
	@test -f /mnt/storagebox/backups/postgres/$$(ls -t /mnt/storagebox/backups/postgres/ | head -1) || (echo "ERROR: No backup found" && exit 1)
	@echo "==> Backup exists: $$(ls -t /mnt/storagebox/backups/postgres/ | head -1)"
	@echo "==> Testing age decrypt..."
	@age -d -i /etc/age.key /mnt/storagebox/backups/postgres/$$(ls -t /mnt/storagebox/backups/postgres/ | head -1) | gunzip | head -c 100

dr-drill-restore:
	@echo "==> Starting DR drill at $$(date -u +%Y%m%dT%H%M%SZ)"
	@echo "==> Step 1: Provision new CAX31 (simulated: use separate staging server)"
	@echo "==> Step 2: Run bootstrap script"
	ssh root@$(DR_TARGET_IP) 'bash -s' < infra/hetzner/cax31-bootstrap.sh
	@echo "==> Step 3: Mount Storage Box"
	ssh root@$(DR_TARGET_IP) 'mount.cifs //u123456.your-storagebox.de/backup /mnt/storagebox ...'
	@echo "==> Step 4: Restore PostgreSQL"
	ssh root@$(DR_TARGET_IP) 'age -d -i /etc/age.key /mnt/storagebox/backups/postgres/$$(ls -t /mnt/storagebox/backups/postgres/ | head -1) | gunzip | docker exec -i meepleai-postgres psql -U meepleai meepleai_db'
	@echo "==> Step 5: Restore Redis"
	ssh root@$(DR_TARGET_IP) 'age -d -i /etc/age.key /mnt/storagebox/backups/redis/$$(ls -t /mnt/storagebox/backups/redis/ | head -1) > /tmp/dump.rdb && docker cp /tmp/dump.rdb meepleai-redis:/data/dump.rdb && docker restart meepleai-redis'
	@echo "==> Step 6: Restore blob storage"
	ssh root@$(DR_TARGET_IP) 'rsync -av /mnt/storagebox/backups/blob/ /var/lib/meepleai/blob/'
	@echo "==> Step 7: Update DNS (Cloudflare)"
	@echo "Manual step: update A record for staging.meepleai.com to $(DR_TARGET_IP)"
	@echo "==> Step 8: Verify health"
	@sleep 30  # Wait for DNS propagation
	curl https://staging.meepleai.com/health || echo "WARNING: Health check failed — see dr-drill-report"

dr-drill-report:
	@echo "==> DR Drill completed at $$(date -u +%Y%m%dT%H%M%SZ)"
	@echo "==> RTO measured: $$(($(DR_END_TIME) - $(DR_START_TIME))) seconds"
```

**Files to create**:
- Modify: `infra/Makefile` — add `dr-drill` target
- Create: `docs/libro-game-assistant/dr-drill-report-template.md` (template)
- Create: `docs/libro-game-assistant/dr-drill-results-[date].md` (filled post-drill)

### Step-by-step DR drill procedure

| Step | Action | Timer starts | Expected duration |
|------|--------|-------------|-------------------|
| T=0 | Begin drill; document start time | Yes | 0 min |
| T+5 | Pre-drill validation: verify backup exists + decrypt test | | 5 min |
| T+10 | Provision new CAX31 on Hetzner (`hcloud server create`) | | 8 min |
| T+18 | SSH available on new server | | Wait for DHCP/SSH |
| T+20 | Run `cax31-bootstrap.sh` | | 15 min |
| T+35 | Mount Storage Box (`mount.cifs`) | | 5 min |
| T+40 | Restore PostgreSQL (decrypt + gunzip + psql pipe) | | 15 min for typical DB size |
| T+55 | Restore Redis (decrypt + docker cp + restart) | | 5 min |
| T+60 | Restore blob storage (`rsync`) | | 5-20 min depending on blob size |
| T+80 | Update Cloudflare DNS A record | | 5 min |
| T+85 | DNS propagation wait (Cloudflare + CDN) | | 5 min |
| T+90 | `curl https://staging.meepleai.com/health` → 200 | | Verify |
| T+95 | Run smoke tests (5 API calls, 1 translation, 1 Q&A) | | 10 min |
| T+105 | Document RTO in post-drill report | | 15 min |
| T+120 | Drill complete; debrief | T=end | Target: ≤ 120 min |

### Validation queries (post-restore integrity checks)

```sql
-- PostgreSQL integrity checks after restore
SELECT COUNT(*) AS user_count FROM auth.users;           -- Expected: >= seeded count
SELECT COUNT(*) AS batch_count FROM public.photo_batch_uploads;  -- Expected: >= seeded count
SELECT COUNT(*) AS chunk_count FROM knowledge_base.text_chunks;  -- Expected: >= seeded count
SELECT MAX(created_at) AS latest_activity FROM auth.users;       -- Expected: within 7 days
-- Check for FK constraint violations
SELECT conname FROM pg_constraint WHERE NOT convalidated;        -- Expected: 0 rows
```

### Post-drill report template fields

```markdown
# DR Drill Results — [Date]

## Summary
- Drill start: [datetime UTC]
- Drill end: [datetime UTC]
- Total RTO: [minutes]
- RTO target: 120 minutes
- Result: PASS / FAIL / MARGINAL

## Steps completed
[checkbox per step above]

## Issues found
[List any blockers encountered, time lost, workarounds applied]

## R2 backup gap
R2 weekly backup not yet implemented (F-5 known gap from Sprint 0).
Evidence: infra/hetzner/backup.cron line 4 commented out.
Recommendation: Implement before launch for offsite redundancy.
Impact if gap unresolved: DR from Storage Box only; no offsite backup for regional outage.

## Validation query results
[Paste query outputs]

## Post-drill cleanup
[Steps taken to clean up the drill CAX31 instance]

## Sign-off
- Executed by: [name]
- Witnessed by: [name]
- Date: [date]
```

**Acceptance criteria**:
- [ ] RTO measured end-to-end ≤ 120 minutes
- [ ] All validation queries pass (zero FK violations, row counts match)
- [ ] Smoke tests pass on restored environment
- [ ] `dr-drill-results-[date].md` published and signed off
- [ ] Vision §6.5 item #8 ("DR drill eseguito, restore < 2h") can be checked off
- [ ] R2 backup gap documented (not blocking launch, but must be in backlog)

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: DR drill completes within RTO target
  Given a valid age-encrypted PostgreSQL backup exists on Storage Box
  And a new CAX31 is provisioned at T=0
  When all DR runbook steps are executed sequentially
  Then GET /health returns Healthy on the new server
  And all validation SQL queries pass
  And total elapsed time is <= 120 minutes

# Edge: backup decryption fails
Scenario: age key mismatch prevents backup decryption
  Given the age private key file is missing from /etc/age.key on the new server
  When the restore attempts to decrypt the backup
  Then the process fails with "ERROR: Failed to decrypt"
  And the drill result is FAIL
  And the recovery action is: copy age.key from secure key storage (1Password/Bitwarden)

# Error: PostgreSQL restore fails due to version mismatch
Scenario: Backup was created with PostgreSQL 16; new instance is PostgreSQL 17
  Given the backup was pg_dump from PostgreSQL 16
  And the new CAX31 pulls PostgreSQL 17 via Docker image :latest
  When psql pipe is executed
  Then restore fails with version warning
  And the recovery action is: pin postgres image to postgres:16 in compose.prod.yml
```

---

## Task 4.4 — Cost telemetry dashboard final review + alerting verification

**Goal**: Verify that the Grafana cost telemetry dashboard correctly reflects LLM cost by use-case (photo processing, translation, Q&A), that all Prometheus alert rules fire correctly under test conditions, and that the on-call playbook for cost surge is documented and validated.

**Owner**: Backend engineer + DevOps
**Effort**: 1.5 days
**Dependency**: Task 4.1a (observability stack running with exporters), Task 4.1b (circuit breaker metrics verified)

### Pre-task: resolve observability gaps from Sprint 0

These known issues (F-3, F-4, F-10) must be resolved before this task can be meaningful:

- [ ] **Exporter gap (F-3)**: postgres-exporter, redis-exporter, node-exporter added (done in Task 4.1a)
- [ ] **Loki schema (F-4)**: Loki pinned to `2.9.10` (done in Task 4.1a)
- [ ] **cost-telemetry.json missing (F-10)**: Create Grafana dashboard file

### Dashboard panels required

The `cost-telemetry.json` Grafana dashboard must include:

| Panel | Prometheus query | Description |
|-------|-----------------|-------------|
| Total LLM cost today (USD) | `meepleai_llm_cost_total{period="today"}` or `sum(increase(meepleai_llm_cost_usd_total[24h]))` | Current day spend |
| Cost by use case | `sum by (use_case) (rate(meepleai_llm_cost_usd_total[1h]))` | photo_processing / translation / qa |
| Cost per session (avg) | `meepleai_llm_cost_usd_total / meepleai_sessions_total` | vs. §4.6 target ≤ €3/session |
| Daily cost trend (7 days) | `sum(increase(meepleai_llm_cost_usd_total[24h]))` over 7d | Trend vs. forecast |
| OpenRouter rate limit events | `meepleai_openrouter_rate_limit_total` | Count of 429s received |
| Circuit breaker state | `meepleai_circuit_breaker_state{service="OpenRouter"}` | 0=Closed, 1=Open |
| Q&A latency P99 | `histogram_quantile(0.99, rate(http_request_duration_seconds_bucket{handler="ask_question"}[5m]))` | vs. target < 5s |
| Translation latency P99 | Same for `handler="translate_paragraph"` | vs. target < 5s |
| Active free users at quota limit | `sum(meepleai_user_quota_status{status="at_limit"})` | Conversion signal |

> Note: Metric names assume gamebook-specific meters are added to `MeepleAiMetrics` class in Phase 1-3. If not yet named exactly as above, align with actual registered meter names. The existing `LlmCostLast24h` double property in `EnhancedServiceDashboardDto.cs` and `ILlmCostLogRepository` confirm the cost tracking infrastructure exists.

### Alert rules to verify

Add to `infra/prometheus-rules.yml`:

```yaml
groups:
  - name: libro_game_cost_alerts
    rules:
      - alert: LlmDailyCostSoftLimit
        expr: sum(increase(meepleai_llm_cost_usd_total[24h])) > 100
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "LLM daily cost exceeded $100 soft limit"
          description: "Daily LLM cost is {{ $value | humanize }} USD. Review usage patterns."
          runbook: "docs/operations/cost-surge-playbook.md"

      - alert: LlmDailyCostHardLimit
        expr: sum(increase(meepleai_llm_cost_usd_total[24h])) > 200
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "LLM daily cost exceeded $200 hard limit — THROTTLE NOW"
          description: "Daily LLM cost is {{ $value | humanize }} USD. Emergency throttling required."
          runbook: "docs/operations/cost-surge-playbook.md"

      - alert: QaLatencyP99High
        expr: histogram_quantile(0.99, rate(http_request_duration_seconds_bucket{handler="ask_question"}[5m])) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Q&A P99 latency exceeds 5s target"

      - alert: PostgresHealthCheckFailing
        expr: meepleai_health_check_status{check="postgres"} == 0
        for: 30s
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL health check failing"
          runbook: "infra/hetzner/disaster-recovery.md"
```

### Alert verification procedure

For each alert rule:

1. **Verify rule loads**: `curl http://prometheus:9090/api/v1/rules` → rule appears in list
2. **Verify alert fires**: inject condition (e.g., artificially increment cost counter), wait `for:` period, confirm alert appears in Alertmanager
3. **Verify alert resolves**: remove condition, confirm alert clears

### Cost surge on-call playbook

File to create: `docs/operations/cost-surge-playbook.md`

Content outline:
1. **Immediate triage** (< 5 min): Check `GET /api/v1/admin/analytics/model-performance` — which use case is spiking?
2. **Identify source**: Is it a single user (quota bypass bug)? A new user wave? A translation loop (infinite retry)?
3. **Emergency throttle** (if > $200/day): Set `OPENROUTER_DAILY_COST_HARD_LIMIT_USD=200` environment variable → restart API container
4. **Communicate**: Post incident in Slack #ops, estimated impact
5. **Root cause**: Within 2h, identify source via Loki logs (`{job="meepleai-api"} |= "llm_cost"`)
6. **Fix + deploy**: Hotfix if bug found; rate limit tightening if user-behaviour-driven
7. **Post-incident report**: Within 24h

**Files to create**:
- Create: `infra/observability/grafana-dashboards/cost-telemetry.json`
- Modify: `infra/prometheus-rules.yml` — add `libro_game_cost_alerts` group
- Create: `docs/operations/cost-surge-playbook.md`

**Acceptance criteria**:
- [ ] Grafana `cost-telemetry.json` dashboard loads with all panels showing data
- [ ] `LlmDailyCostSoftLimit` alert fires correctly when cost threshold is exceeded in test
- [ ] `LlmDailyCostHardLimit` alert fires correctly
- [ ] `QaLatencyP99High` alert fires correctly when latency injection used (chaos setup from 4.1b)
- [ ] `PostgresHealthCheckFailing` alert fires correctly (validate during Task 4.1d)
- [ ] Cost per session shown in dashboard is ≤ €3.00 in steady-state load test (vision §6.3 criterion 3)
- [ ] On-call playbook published and reviewed by Aaron

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: Cost telemetry dashboard shows real data
  Given the observability stack is running with postgres/redis/node exporters
  And the API has processed at least 10 translation requests
  When I open the cost-telemetry Grafana dashboard
  Then all panels show non-zero data
  And cost-per-session panel shows a value <= 3.00 EUR

# Edge: cost alert fires and auto-resolves
Scenario: Soft cost alert fires then resolves
  Given the LlmDailyCostSoftLimit alert rule is loaded in Prometheus
  When I inject a synthetic cost counter increase above $100
  And wait 5 minutes for the "for:" period
  Then the alert fires in Alertmanager
  When I remove the synthetic increase
  Then the alert resolves within 1 minute

# Error: dashboard has no data (exporter not running)
Scenario: Missing postgres-exporter causes empty panels
  Given postgres-exporter is not running
  When I open the cost-telemetry dashboard
  Then the DB-derived panels show "No data"
  And I add the postgres-exporter service to compose.observability.yml
  And redeploy
  Then the panels show PostgreSQL metrics
```

---

## Task 4.5 — Final launch checklist

**Goal**: Verify that all 8 pre-launch prerequisites from vision §6.5 have documented evidence from prior tasks, all P0/P1 usability issues are resolved, and every owner signs off on their item. This task produces no new code — it is a verification and sign-off process.

**Owner**: Product owner (Aaron) + Task leads per item
**Effort**: 0.5 days (sign-off meeting)
**Dependency**: ALL prior tasks in Phases 1-4 must be complete OR explicitly scoped-out with product owner approval

### Launch checklist (vision §6.5 — 8 items)

| # | Vision §6.5 item | Owner | Evidence required | Produced by | Status |
|---|-----------------|-------|------------------|-------------|--------|
| 1 | **PR-1**: Legal review copyright + TOS aggiornato + privacy policy GDPR-compliant | Legal advisor (Aaron responsible) | Signed legal review document + TOS live at `/terms` + Privacy policy live at `/privacy` with gamebook disclosure section | Phase 0 (PR-1 started) + Task 3.7 (privacy page gamebook section) | Must confirm externally |
| 2 | **PR-2**: OCR validated su ≥ 5 manuali gamebook reali (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor, 7th Continent) | ML engineer | `tests/llm-eval/ocr-validation/results.md` with ≥ 85% pages confidence ≥ 0.7 for each game, across 3 lighting conditions | Task 0.1 (OCR validation sprint) | Blocked on physical manuals |
| 3 | **PR-3**: Test set golden 100 Q&A + 50 paragrafi narrativi italiani con expert validation | ML engineer + IT native speaker contractor | `tests/llm-eval/golden-set/golden-qa.jsonl` (100 rows) + `golden-translation.jsonl` (50 rows), both with `validated_by` field | Task 0.2 (golden set creation) | In progress |
| 4 | **CAX31 deployment** + monitoring + alerting + backup automated | DevOps | Staging DR drill report (`dr-drill-results-[date].md`) passes; production deploy workflow active; backup cron running on production | Tasks 4.1a, 4.3, 4.6 | Phase 4 |
| 5 | **Pricing engine tested** end-to-end (Free counter + Credits checkout + Stripe) | Backend engineer | Phase 3 acceptance gate E2E test results; `stripe-integration-test.md` with webhook + idempotency evidence | Task 3.9 acceptance gate | Phase 3 |
| 6 | **Hallucination rate ≤ 3%** validato su test set golden in CI gate | ML engineer | CI pipeline shows `hallucination-gate` job passing (Task 2.7); `hallucination-rate: X%` in PR check output | Task 2.7 (CI gate) | Phase 2 |
| 7 | **5 sessioni usability testing** completate, feedback incorporato | UX researcher + Frontend | `docs/libro-game-assistant/usability-results-summary.md` published; all P0/P1 issues from sessions closed as GitHub issues marked "fixed" | Task 4.2 | Phase 4 |
| 8 | **Disaster recovery drill** eseguito (restore da backup in < 2h) | DevOps | `docs/libro-game-assistant/dr-drill-results-[date].md` signed; RTO ≤ 120 min confirmed | Task 4.3 | Phase 4 |

### Sign-off matrix

| Item | Must sign off | Deadline (relative to launch date) |
|------|--------------|-------------------------------------|
| PR-1 Legal | Legal advisor + Aaron | Launch date - 14 days |
| PR-2 OCR | ML lead | Launch date - 7 days |
| PR-3 Golden set | ML lead + native speaker contractor | Launch date - 7 days |
| CAX31 + monitoring | DevOps lead | Launch date - 3 days |
| Pricing engine | Backend lead + Aaron | Launch date - 3 days |
| Hallucination gate | ML lead | Launch date - 7 days (gate must be in CI for ≥ 1 week) |
| Usability results | UX researcher + Aaron | Launch date - 5 days |
| DR drill | DevOps lead + Aaron | Launch date - 5 days |

### New gates from Phase 4 findings

In addition to the 8 vision §6.5 items, the following Phase 4 gates are required:

| ID | Gate | Evidence | Owner |
|----|------|---------|-------|
| G4-1 | All 4 chaos tests (4.1a-d) passed with documented results | `chaos-4-1[b-d]-results.md` each showing PASS | DevOps |
| G4-2 | PR-1 legal dependency confirmed (R-27 risk) | Legal advisor email confirmation | Aaron |
| G4-3 | R2 weekly backup gap documented in backlog (not blocking, but explicit) | GitHub issue "implement R2 weekly backup" with priority label | DevOps |
| G4-4 | `deploy-production.yml` re-enabled and smoke-tested on production-mirror | Workflow run log showing successful deploy to production-mirror | DevOps |
| G4-5 | Cost surge playbook reviewed by Aaron | Aaron sign-off on `docs/operations/cost-surge-playbook.md` | Aaron + DevOps |
| G4-6 | Loki and exporter drift from Sprint 0 resolved | `compose.observability.yml` showing pinned Loki + exporter services | DevOps |

**Acceptance criteria** (Task 4.5 is complete when):
- [ ] All 8 vision §6.5 checklist items have documented evidence (or explicit product owner approval to launch with known gap + post-launch plan)
- [ ] All 6 new Phase 4 gates passed
- [ ] Sign-off matrix fully signed
- [ ] No open "launch-blocker" GitHub issues

---

## Task 4.6 — Production deploy + 1-week monitoring

**Goal**: Deploy the MVP to production Hetzner CAX31 (meepleai.com), execute smoke tests, and monitor for 7 days, escalating any P0 incidents within 30 minutes.

**Owner**: DevOps lead + Backend lead
**Effort**: 0.5 days deploy + 7 days monitoring (passive, with alerting)
**Dependency**: Task 4.5 fully signed off (all gates passed); `deploy-production.yml.disabled` reviewed and re-enabled

### Pre-deploy checklist

- [ ] All Task 4.5 sign-offs complete
- [ ] `deploy-production.yml.disabled` renamed to `deploy-production.yml` and reviewed for gamebook services (smoldocling-service, embedding-service included in service list)
- [ ] Production secrets in `infra/secrets/prod/` complete (openrouter.secret, stripe.secret, etc.)
- [ ] EF Core migration dry-run completed on production-schema clone: `dotnet ef migrations script` reviewed and approved
- [ ] Cloudflare DNS points to production CAX31 (not staging)
- [ ] Backup cron running on production server (`crontab -l` shows `backup.sh` at 03:00 UTC)
- [ ] i18n CI gate passing (plan v2 self-review note §"NEW: i18n CI gate")

### Service deployment order (Sam Newman: dependency ordering)

Production is a single CAX31 with Docker Compose. All services start via Compose, but health checks enforce ordering:

1. **postgres** (depends_on: nothing) — waits for `pg_isready`
2. **redis** (depends_on: nothing)
3. **embedding-service** (depends_on: nothing — Python service)
4. **smoldocling-service** (depends_on: nothing — Python service)
5. **api** (depends_on: postgres healthy + redis healthy)
6. **web** (depends_on: api healthy)
7. **traefik** (depends_on: web + api)
8. **observability stack** (depends_on: api — scrapes `/metrics`)

### Deployment steps

- [ ] **Step 1**: Tag release in git: `git tag mvp-phase-1-complete && git push origin mvp-phase-1-complete`
- [ ] **Step 2**: Trigger `deploy-production.yml` via `workflow_dispatch` on GitHub Actions
- [ ] **Step 3**: Monitor deploy: watch GitHub Actions logs for each step
- [ ] **Step 4**: Run EF Core migrations on production: `dotnet ef database update` (or via deploy workflow migration step)
- [ ] **Step 5**: Execute production smoke tests (see below)
- [ ] **Step 6**: Verify Prometheus scraping production `/metrics` in Grafana
- [ ] **Step 7**: Verify backup cron: manually trigger `backup.sh` and confirm file appears on Storage Box
- [ ] **Step 8**: Enable Alertmanager notifications (configure `infra/alertmanager.yml` with email/Slack webhook)
- [ ] **Step 9**: Announce launch (internal communication first, then public)

### Production smoke tests (post-deploy gate)

Execute manually or via automated smoke test script within 30 minutes of deploy:

| # | Test | Command | Expected result |
|---|------|---------|----------------|
| S1 | API health | `curl https://api.meepleai.com/health` | `200 OK`, all checks Healthy |
| S2 | Web loads | `curl -L https://meepleai.com` | HTML response, no 500 |
| S3 | Auth flow | Login with test account | Session created, redirect to dashboard |
| S4 | Photo upload (1 image) | Upload 1 photo from staging demo set | `202 Accepted`, job queued |
| S5 | Q&A (from pre-indexed game) | Ask rule question | Response with citation in < 5s |
| S6 | Translation (1 paragraph) | Request translation | Response in < 5s |
| S7 | Pricing quota check | Check free tier counter | Returns `{"used":0,"limit":50,"unit":"pages"}` |
| S8 | Observability | `curl https://api.meepleai.com/metrics` | Prometheus text format, non-empty |

### Rollback strategy

| Scenario | Rollback action | RTO |
|----------|----------------|-----|
| Deploy fails mid-way | `docker compose down; docker compose up -d --scale api=0` (keep DB); restore previous image tag | 10 min |
| EF migration fails | Run `dotnet ef migrations script --idempotent` to identify failed migration; manually revert via SQL `BEGIN; DELETE FROM __EFMigrationsHistory WHERE MigrationId = '...'; ROLLBACK;` then `docker compose restart api` | 30 min |
| Critical P0 bug found post-launch | `docker pull ghcr.io/meepleai/api:<previous-tag>; docker compose up -d --no-deps api` | 15 min |
| Cost surge detected (> $200/day) | Set `OPENROUTER_DAILY_COST_HARD_LIMIT_USD=50` as emergency brake; `docker compose restart api` | 5 min |

### 7-day post-launch monitoring plan

**Day 1-2 (active monitoring)**:
- Engineering team checks Grafana every 2 hours
- Alert response SLA: P0 within 30 min, P1 within 2h
- Watch for: cost surge, OOM-killer (check `dmesg | grep kill`), translation error rate

**Day 3-7 (passive monitoring)**:
- Alertmanager handles notifications
- Daily morning check: Grafana dashboard review (5 min)
- End-of-week report: sessions started, cost per session, error rates, top Q&A topics

**KPIs to track against vision §6.3**:
- Sessions started (target: ≥ 5 real sessions)
- Session completion rate ≥ 2h (target: ≥ 70%)
- Cost per session (target: ≤ €3.00)
- Q&A P99 latency (target: < 5s)
- Translation P99 latency (target: < 5s)
- Hallucination reports from users (target: 0 critical)
- Free tier conversions to Credits (leading indicator)

### i18n CI gate

Per plan v2 self-review note: the i18n CI gate must be passing before launch. The gate checks that all UI strings are extracted (no hardcoded Italian strings in JSX), i18n keys are present in `it.json`, and no keys are missing in other locale files. This must be part of the CI workflow or a pre-deploy check.

**Files to create/modify**:
- Rename: `.github/workflows/deploy-production.yml.disabled` → `deploy-production.yml`
- Modify: `deploy-production.yml` — add gamebook services (smoldocling, embedding), add migration step, add smoke test step
- Create: `infra/scripts/smoke-tests.sh` (automates S1-S8 above)
- Create: `docs/libro-game-assistant/launch-monitoring-week1.md` (7-day KPI tracking template)

**Acceptance criteria**:
- [ ] All S1-S8 smoke tests pass within 30 minutes of deploy
- [ ] Grafana shows production data within 15 minutes of deploy
- [ ] Backup cron confirmed running on production
- [ ] Zero P0 incidents in first 24 hours
- [ ] Vision §6.3 acceptance criteria 1-8 all measurable within 7 days

**Gherkin scenarios**:

```gherkin
# Happy path
Scenario: Successful production deploy with all smoke tests passing
  Given Task 4.5 is fully signed off
  And deploy-production.yml is enabled
  When I trigger the workflow via workflow_dispatch
  Then all Docker containers start in dependency order
  And EF Core migrations complete without error
  And all S1-S8 smoke tests pass within 30 minutes
  And Grafana shows live production metrics

# Edge: EF migration fails on production
Scenario: Rollback after migration failure
  Given a new migration was added in Phase 3
  And the migration has a bug (column type mismatch)
  When the migration runs on production PostgreSQL
  Then dotnet ef database update returns non-zero exit code
  And the deploy workflow marks the job as failed
  And I run the rollback procedure (delete from __EFMigrationsHistory)
  And redeploy with the previous API image tag

# Error: Cost surge detected on Day 1
Scenario: Emergency throttle activated after cost surge
  Given production is live and receiving first real users
  And LlmDailyCostHardLimit alert fires ($200/day exceeded)
  When on-call engineer receives alert
  Then they set OPENROUTER_DAILY_COST_HARD_LIMIT_USD=50 and restart API
  And cost spike stops within 5 minutes
  And post-incident report is filed within 24h
```

---

## Cross-cutting concerns

### Production-like staging definition

> **Normative definition** for use in all Phase 4 tasks.

"Production-like" means: a dedicated Hetzner CAX31 (NOT the developer's local machine) running the IDENTICAL compose stack as production (`compose.prod.yml` + `compose.traefik.yml` + `observability/compose.observability.yml`) with ARM64 architecture, production-equivalent secret values (real API keys, not placeholders), a PostgreSQL database seeded with ≥ 5 users and ≥ 2 gamebooks each with ≥ 20 photos indexed (approximately 1/10 of expected production data at launch), the backup script running with ≥ 1 successful daily backup on Storage Box, all health checks green, and all Prometheus exporters scraping successfully.

**NOT acceptable** as "production-like": Docker Desktop on developer laptop, compose.dev.yml, placeholder secrets, no backup, Prometheus showing phantom targets, Loki failing silently on schema incompatibility.

### Observability gate (test the test)

Every chaos test requires that the relevant observability signal fires BEFORE the test is considered passed. This is the "test the test" principle — a chaos test that passes without a corresponding observable signal in Prometheus/Grafana provides no confidence.

| Chaos test | Required observability signal before PASS |
|-----------|------------------------------------------|
| 4.1b OpenRouter 429 | `circuit_breaker_state{service="OpenRouter"}` = Open in Prometheus |
| 4.1c WiFi loss | Application log shows "Network unavailable" warning in Loki |
| 4.1d PostgreSQL failover | `meepleai_health_check_status{check="postgres"}` = 0 in Prometheus |
| 4.3 DR drill | `curl https://staging.meepleai.com/health` returns Healthy on restored server |
| 4.4 alert rules | Each Alertmanager alert fires correctly under test injection |

### Rollback strategy matrix

| Task | Rollback | Scope | Time |
|------|----------|-------|------|
| 4.1a staging provision | `hcloud server delete meepleai-staging` (not production) | Staging only | 5 min |
| 4.1b WireMock | `docker compose -f chaos/compose.wiremock.yml down` + remove OPENROUTER_BASE_URL override | Staging only | 2 min |
| 4.1c network loss | `sudo iptables -F OUTPUT` or Chrome DevTools → Online | Staging only | 30 sec |
| 4.1d PostgreSQL stop | `docker compose start postgres` | Staging only | 30 sec |
| 4.3 DR drill | `hcloud server delete meepleai-dr-test` (drill server, not production) | Drill server only | 5 min |
| 4.6 production deploy | Revert to previous image tag + `docker compose up -d --no-deps api` | Production | 15 min |
| 4.6 EF migration | Manual SQL revert + restart API (see Task 4.6 rollback table) | Production | 30 min |

### Compliance signoff dependency (PR-1)

PR-1 (Legal review copyright + TOS + GDPR-compliant privacy policy) is a hard dependency on Task 4.6 (production deploy). The compliance signoff must be complete before the launch checklist can be signed off (Task 4.5), which is in turn a dependency for Task 4.6.

**Risk R-27** (PR-1 not complete by launch) is rated Critical. Mitigation: Aaron must confirm legal advisor engagement status by Week 19 Day 1. If not confirmed, the Phase 4 timeline must slip rather than launch without legal review.

The specific gamebook-relevant legal question (PR-1 core scope): Is AI-assisted indexing + Q&A of physically-owned board game manuals (user-owned copy, not redistributed) covered by EU copyright exceptions (Art. 5 InfoSoc Directive — reproduction for private use / text-and-data mining)? The legal advisor must opine on this before user-generated content (indexed manuals) is accepted in production.

### Cost monitoring early-user surge

**Forecast (from vision §4.6)**: Cost target ≤ €3.00 per 2h session. With free tier of 50 pages/month, expected at launch: 50-100 MAU, avg 2 sessions/user/month = 100-200 sessions/month = €300-600/month in LLM costs at target.

**Variance threshold**: If Day 1-3 cost-per-session > €5.00 (67% over target), trigger the cost surge on-call playbook immediately. Do not wait for the $200/day hard limit alert.

**Early-warning indicators** (Grafana panels from Task 4.4):
1. Cost per session > €5: investigate translation call frequency (are users requesting full-chapter translation instead of single paragraphs?)
2. OpenRouter rate limit events > 0: check if daily limits are set too low for legitimate traffic
3. Free tier users at quota limit > 10% of MAU on Day 1: may indicate quota is too restrictive for adoption

**Unanticipated user behaviour risks**:
- Users photographing entire books (100+ pages) on free tier — handled by 50-page/month hard cap, but batch processing cost may spike before cap is enforced
- Users asking the same Q&A question repeatedly (cache miss on paraphrase variants) — `ISemanticResponseCache` should handle this but verify cache hit rate in Grafana
- Translation of entire chapters in rapid succession — per-paragraph rate limiting in `IUserRateLimiter` must be active from day 1

---

## MVP Launch Acceptance Gate

### Vision §6.3 acceptance criteria (8 items)

| # | Criterion | Evidence task | Measurable in production |
|---|-----------|--------------|--------------------------|
| 1 | 5 sessioni reali end-to-end completate con gruppi target (gamebook EN tradotto) | Task 4.2 usability sessions + 7-day post-launch monitoring | ✅ Usability results + production session logs |
| 2 | ≥ 70% utenti completano almeno 1 sessione di 2h+ | Task 4.2 + Task 4.6 Day 1-7 monitoring | ✅ KPI from `launch-monitoring-week1.md` |
| 3 | Costo medio sessione ≤ €3.00 | Task 4.4 cost dashboard + Task 4.6 monitoring | ✅ `cost-per-session` Grafana panel |
| 4 | Hallucination rate Q&A ≤ 3% su test set golden (PR-3) | Task 2.7 CI gate | ✅ CI pass evidence |
| 5 | OCR validation su 5 manuali ≥ 85% pages confidence accettabile (PR-2) | Task 0.1 OCR validation | ✅ `tests/llm-eval/ocr-validation/results.md` |
| 6 | Legal review copyright completata + TOS aggiornato (PR-1) | External legal advisor | ✅ Signed legal document |
| 7 | Latenza P95 Q&A < 5 sec, traduzione < 5 sec | Task 4.4 Grafana P99 panels + Task 4.6 smoke tests | ✅ Prometheus histograms |
| 8 | Pricing engine 2-tier funzionante con cap free 50 pag/mese | Task 3.9 acceptance gate | ✅ E2E test results |

### Vision §6.5 pre-launch prerequisites (8 items)

| # | Prerequisite | Evidence task | Sign-off owner |
|---|-------------|--------------|----------------|
| 1 | PR-1: Legal + TOS + privacy policy GDPR-compliant | Phase 0 + Task 3.7 | Legal advisor + Aaron |
| 2 | PR-2: OCR validated ≥ 5 manuali | Task 0.1 | ML lead |
| 3 | PR-3: Test set golden 100 Q&A + 50 paragrafi | Task 0.2 | ML lead + contractor |
| 4 | CAX31 deployment + monitoring + alerting + backup automated | Task 4.1a + 4.4 + 4.6 | DevOps lead |
| 5 | Pricing engine tested end-to-end | Task 3.9 | Backend lead |
| 6 | Hallucination rate ≤ 3% in CI gate | Task 2.7 | ML lead |
| 7 | 5 sessioni usability testing completate | Task 4.2 | UX researcher + Aaron |
| 8 | DR drill eseguito (restore < 2h) | Task 4.3 | DevOps lead + Aaron |

### New gates from Phase 4 chaos/DR/usability findings

| ID | Gate | Evidence |
|----|------|---------|
| G4-1 | All 4 chaos tests passed with PASS status in results docs | `chaos-4-1[b-d]-results.md` |
| G4-2 | PR-1 legal dependency confirmed 14 days before launch | Legal advisor written confirmation |
| G4-3 | R2 backup gap backlogged (not blocking) | GitHub issue created with `post-launch` milestone |
| G4-4 | `deploy-production.yml` smoke-tested on production-mirror | CI run log |
| G4-5 | Cost surge playbook reviewed and approved | Aaron sign-off |
| G4-6 | Sprint 0 observability drift resolved (exporters, Loki pin) | `compose.observability.yml` diff in PR |

### Sign-off matrix

| Gate | Owner | Evidence file | Deadline |
|------|-------|--------------|----------|
| PR-1 Legal | Legal advisor | Legal review document (external) | Launch - 14 days |
| PR-2 OCR | ML lead | `tests/llm-eval/ocr-validation/results.md` | Launch - 7 days |
| PR-3 Golden set | ML lead | `tests/llm-eval/golden-set/` JSONL files | Launch - 7 days |
| Hallucination CI gate | ML lead | CI workflow green on `main-dev` | Launch - 7 days |
| Usability results | UX researcher | `usability-results-summary.md` | Launch - 5 days |
| DR drill | DevOps lead | `dr-drill-results-[date].md` | Launch - 5 days |
| Chaos tests | DevOps lead | `chaos-4-1[b-d]-results.md` | Launch - 5 days |
| Cost telemetry | DevOps + Aaron | Grafana screenshot + alert test evidence | Launch - 3 days |
| CAX31 production ready | DevOps lead | Production health check green | Launch - 2 days |
| Deploy smoke tests | Backend lead | `smoke-tests.sh` run log | Launch day |

---

## Effort estimate

| Task | Effort | Owner | Dependencies |
|------|--------|-------|-------------|
| **4.1a** Production-like staging env | 2 days | DevOps | Phase 3 complete |
| **4.1b** WireMock OpenRouter chaos | 1.5 days | Backend | 4.1a |
| **4.1c** WiFi loss simulation | 1.5 days | Frontend + DevOps | 4.1a, Phase 3 UI |
| **4.1d** PostgreSQL failover | 1 day | Backend + DevOps | 4.1a |
| **4.2** Usability testing 5 sessions | 2 weeks calendar (1 wk recruit + 1 wk sessions) | UX researcher | Phase 3 UI deployed |
| **4.3** DR drill | 1.5 days | DevOps + Backend | 4.1a, backup running |
| **4.4** Cost telemetry review | 1.5 days | Backend + DevOps | 4.1a, 4.1b (for alert test) |
| **4.5** Launch checklist sign-off | 0.5 days | Aaron + all leads | All above complete |
| **4.6** Production deploy + monitoring | 0.5 days deploy + 7 days passive | DevOps + Backend | 4.5 signed off |
| **Buffer** (usability hotfixes, chaos discoveries) | 3 days | Engineering | Per findings |

**Total Phase 4**: ~4 calendar weeks (Weeks 19-22)

**Parallelism opportunities**:
- Task 4.2 (usability, 2 weeks calendar) runs in parallel with Tasks 4.1a-d (chaos, ~6 days engineering)
- Task 4.4 (cost dashboard) can start in parallel with Task 4.3 (DR drill) once 4.1a is complete
- Task 4.5 (checklist) happens after all others complete; 0.5 days only

**Effort verdict**: **TIGHT but feasible** with disciplined parallelism (chaos + usability simultaneously) and no scope creep. The 3-day buffer is minimal. The following risks can break the 4-week budget:

1. Usability sessions reveal P0/P1 issues requiring more than 3 days of fixes
2. DR drill discovers backup corruption (R-23) requiring R2 backup implementation
3. PR-1 legal review not started by Week 19 (R-27) blocks Task 4.5

If any of these risks materialise, Phase 4 extends to 5-6 calendar weeks. This should be communicated to stakeholders at the start of Week 19.

---

## Documents created by Phase 4

| File | Created by | Purpose |
|------|-----------|---------|
| `infra/observability/compose.observability.yml` | Task 4.1a (modified) | Add exporters, pin Loki |
| `infra/hetzner/staging-provision.sh` | Task 4.1a | Staging CAX31 bootstrap |
| `infra/scripts/k6-steadystate-load.js` | Task 4.1a | Steady-state load simulation |
| `docs/libro-game-assistant/chaos-steady-state-baseline.md` | Task 4.1a | Baseline metrics |
| `infra/chaos/wiremock-openrouter-429.json` | Task 4.1b | WireMock stub |
| `infra/chaos/compose.wiremock.yml` | Task 4.1b | WireMock compose |
| `infra/chaos/run-chaos-4-1b.sh` | Task 4.1b | Chaos automation |
| `docs/libro-game-assistant/chaos-4-1b-results.md` | Task 4.1b | Test results |
| `infra/chaos/run-chaos-4-1c.sh` | Task 4.1c | Network chaos automation |
| `docs/libro-game-assistant/chaos-4-1c-results.md` | Task 4.1c | Test results |
| `infra/chaos/run-chaos-4-1d.sh` | Task 4.1d | PG failover automation |
| `docs/libro-game-assistant/chaos-4-1d-results.md` | Task 4.1d | Test results |
| `docs/libro-game-assistant/usability-test-protocol.md` | Task 4.2 | UX test protocol |
| `docs/libro-game-assistant/usability-results-summary.md` | Task 4.2 | UX findings |
| `infra/Makefile` (modified) | Task 4.3 | `dr-drill` target |
| `docs/libro-game-assistant/dr-drill-report-template.md` | Task 4.3 | DR drill template |
| `docs/libro-game-assistant/dr-drill-results-[date].md` | Task 4.3 | DR drill results |
| `infra/observability/grafana-dashboards/cost-telemetry.json` | Task 4.4 | Grafana dashboard |
| `infra/prometheus-rules.yml` (modified) | Task 4.4 | Alert rules |
| `docs/operations/cost-surge-playbook.md` | Task 4.4 | On-call runbook |
| `.github/workflows/deploy-production.yml` | Task 4.6 (rename) | Production deploy |
| `infra/scripts/smoke-tests.sh` | Task 4.6 | Post-deploy smoke tests |
| `docs/libro-game-assistant/launch-monitoring-week1.md` | Task 4.6 | 7-day KPI tracking |
