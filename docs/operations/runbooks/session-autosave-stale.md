# Runbook: SessionAutoSaveJobStale

> **Alert**: `SessionAutoSaveJobStale`
> **Severity**: warning
> **Category**: `background_jobs` / `session_tracking`
> **Source file**: [`infra/prometheus-rules.yml`](../../../infra/prometheus-rules.yml) (group `meepleai_warning_alerts`)
> **Metric**: `meepleai_session_autosave_last_run_age_seconds`
> **First added**: 2026-04-09 (follow-up to Issue #301 / PR #283 F3)

## What this alert means

The `AutoSaveSessionJob` (a Quartz.NET background job) has not executed in more than **120 seconds**, while the expected cadence is every **60 seconds**. This means **at least two scheduled ticks have been missed**, and any game-night sessions currently in progress are at risk of losing unsaved state.

### Source metric

The gauge is published from `Api.Observability.MeepleAiMetrics.RegisterAutoSaveHealthGauge`, which reads from the singleton `IAutoSaveHealthTracker`:

- `RecordRun()` is called at the end of each successful `AutoSaveSessionJob.Execute()`
- `GetLastRunAgeSeconds()` returns `null` at startup (before the first run) → the gauge emits **`-1`** as a sentinel
- When the tracker has at least one recorded run, the gauge returns seconds since that run

The alert expression explicitly excludes the `-1` sentinel so it does **not** fire during cold starts or in environments where no game-night sessions are active:

```promql
meepleai_session_autosave_last_run_age_seconds > 120
and
meepleai_session_autosave_last_run_age_seconds >= 0
```

### Related in-process signal

`AutoSaveHealthLoggerService` (a `BackgroundService`) polls the same tracker every 30 seconds and emits a log-level warning when `ageSeconds > 120`. The Prometheus alert and the log warning fire in lockstep — they are two routing surfaces for the same underlying signal.

## Impact

- **Direct**: In-progress game-night sessions will not be auto-saved. If the API process is restarted or crashes, players will lose state back to the previous successful save.
- **Indirect**: The AI agent memory (house rules, session notes) may also be stale if its persistence depends on the same job.
- **User-visible**: None until the user tries to resume a session after a crash and discovers missing turns / scores.

## Triage checklist

Work through these in order. Each step is ~30–60 seconds.

### 1. Confirm the alert is not a false positive

- [ ] Check the metric in Grafana or query directly:
  ```bash
  curl -s http://api:8080/metrics | grep meepleai_session_autosave_last_run_age_seconds
  ```
- [ ] If the value is `-1`, the tracker has never recorded a run since process start. This is expected on a fresh boot or in an environment with no active sessions — the alert's `>= 0` guard should have suppressed it. If you're seeing the alert fire on `-1`, there is a **bug in the alert expression** — escalate.
- [ ] If the value is a large positive number (e.g. > 300), proceed to step 2.

### 2. Check Quartz scheduler health

The job is scheduled by `QuartzAutoSaveSchedulerService` and registered in `SessionTrackingServiceExtensions.cs`. Verify:

- [ ] The Quartz scheduler is running and the `IAutoSaveSchedulerService` is registered in DI.
- [ ] No recent Quartz errors in the API logs:
  ```bash
  pwsh -c "docker logs meepleai-api --tail=500 | Select-String -Pattern 'AutoSave|Quartz'"
  ```
- [ ] The `AutoSaveHealthLoggerService` warning line (`"AutoSave job stale: last run {AgeSeconds} seconds ago"`) should appear in logs near the alert firing time. If not, the logger service itself may have crashed.

### 3. Check for blocking work on the job thread

The auto-save command (`AutoSaveSessionCommand`) hits the database and the session store. A stuck DB query, pgvector hotspot, or Redis latency can delay the job:

- [ ] Check Postgres for long-running queries:
  ```sql
  SELECT pid, query_start, state, query
  FROM pg_stat_activity
  WHERE state != 'idle' AND query_start < now() - interval '30 seconds'
  ORDER BY query_start;
  ```
- [ ] Check Redis responsiveness: `docker exec meepleai-redis redis-cli --latency-history`

### 4. Check for missing DI registrations

Two known root causes for silent job stalls:

- [ ] `IAutoSaveSchedulerService` not registered → the job never fires. Verify in `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/SessionTrackingServiceExtensions.cs`.
- [ ] `IAutoSaveHealthTracker` registered as transient instead of singleton → the tracker instance observed by the gauge is different from the one the job writes to. Must be `AddSingleton`.

### 5. Last resort: restart the API

If triage does not reveal the root cause within a few minutes and active sessions are at risk:

```bash
cd infra && pwsh -c "docker compose restart api"
```

The alert will clear once the first successful `AutoSaveSessionJob.Execute()` runs and updates the tracker.

## What does NOT clear this alert

- Restarting Prometheus (the metric is exported by the API, not Prometheus)
- Restarting Alertmanager (this only resets alert routing state, not the underlying gauge)
- Scaling the API horizontally (the gauge is per-process; a second replica would publish its own value)

## Observability references

- **Metric source**: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.SessionAutoSave.cs`
- **Tracker**: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthTracker.cs`
- **Log-level signal**: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthLoggerService.cs`
- **Quartz scheduler**: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/QuartzAutoSaveSchedulerService.cs`
- **Job**: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/AutoSaveSessionJob.cs`

## Dashboard

The alert currently references `http://localhost:3001/d/api-performance` as a placeholder. There is **no dedicated background-jobs dashboard yet** — this is tracked as a follow-up. Until then, use Prometheus directly:

```
http://prometheus:9090/graph?g0.expr=meepleai_session_autosave_last_run_age_seconds
```

## Related

- PR #283 (F3 follow-up): introduced the gauge and the log-level signal
- PR #327: promoted the gauge to a routable Prometheus alert (this runbook)
- Issue #301: spec-panel review that flagged the observability gap
