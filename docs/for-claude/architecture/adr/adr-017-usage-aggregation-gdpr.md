# ADR-017: User AI Usage Aggregation vs GDPR Pseudonymization

**Date**: 2026-03-10
**Status**: Proposed
**Deciders**: Spec Panel (Nygard, Fowler, Wiegers, Crispin)
**Context**: Epic #25 (C3) vs Epic F (F5)

## Context

Two requirements conflict:

1. **Epic F, F5**: GDPR compliance requires pseudonymizing `UserId` in `llm_request_logs` after 7 days (replace with SHA-256 hash). This is implemented via `LlmRequestLogPseudonymizationJob` running daily at 2:00 AM UTC.

2. **Epic #25, C3**: Editors need a self-service page showing their personal AI usage for the last 30 days — requiring per-user queries on data older than 7 days.

After pseudonymization, `UserId` becomes an irreversible hash. The editor's real UUID cannot match against hashed records. **Result**: Only 7 of 30 days would be queryable by user.

## Decision

**Introduce a pre-aggregated `UserAiUsageDailySummary` table** that stores daily per-user totals, computed from raw logs BEFORE pseudonymization runs.

### Two-Table Architecture

```
                    1:30 AM              2:00 AM              4:00 AM
                   Aggregation      Pseudonymization         Cleanup
                       │                   │                    │
Raw Logs ─────────────►├──► Summaries      │                    │
(llm_request_logs)     │   (aggregated)    ├──► Hash UserId     ├──► Delete expired
                       │                   │                    │
```

| Aspect | Raw Logs | Usage Summaries |
|--------|----------|-----------------|
| **Purpose** | Audit trail, admin debug | Editor self-service analytics |
| **Granularity** | Per-request (full detail) | Per-user per-day (aggregated) |
| **PII content** | Yes (UserId, prompt context via session) | Minimal (UserId + counts only) |
| **Pseudonymization** | After 7 days (GDPR F5) | Not needed (see rationale) |
| **Retention** | 30 days (hard delete) | 90 days |
| **Query pattern** | Admin: filter by source/model/date | Editor: filter by own UserId + date range |

### GDPR Compliance Rationale

Aggregated daily summaries containing only:
- `UserId` (identifier)
- `Date` (day)
- `RequestCount`, `TotalTokens`, `CostUsd` (numeric aggregates)
- `ModelDistribution`, `StrategyDistribution` (categorical counts)

...do **not** reveal the content of communications or enable profiling of individual behavior beyond basic usage metrics. Under GDPR Art. 5(1)(e) (storage limitation), retention of aggregate usage metrics for 90 days is proportionate to the legitimate interest of providing usage visibility to data subjects themselves (Art. 6(1)(f)).

However, as a precaution:
- Summaries are deleted after 90 days (not retained indefinitely)
- The `DeleteUserLlmDataCommand` (F3) must also delete usage summaries for the user
- Users can request deletion of their usage summaries via standard GDPR erasure flow

## Consequences

### Positive
- Editors get 30-day usage visibility without compromising GDPR pseudonymization
- Clear separation: raw logs for audit, summaries for analytics
- Aggregation job is idempotent (UPSERT) — safe to re-run
- No changes to existing pseudonymization or cleanup jobs

### Negative
- New table + migration + background job to maintain
- Small window of data loss risk: if aggregation job fails, that day's data is lost after pseudonymization (mitigation: alerting on job failure + catch-up logic)
- `DeleteUserLlmDataCommand` must be extended to include the new table

### Neutral
- Existing admin analytics continue to work unchanged (they query raw logs)
- No impact on SSE streaming or real-time features

## Alternatives Considered

### A: Extend pseudonymization to 30 days
**Rejected**: Weakens GDPR compliance. 7-day pseudonymization was chosen based on data minimization principle.

### B: Store usage counters in Redis
**Rejected**: Redis is volatile (restart = data loss). Not suitable for 30-day historical data.

### C: Maintain a separate "analytics UserId" not subject to pseudonymization
**Rejected**: Defeats the purpose of pseudonymization. Two non-hashed copies = no real anonymization.

### D: Let editors only see 7 days of detailed data
**Rejected**: PRD explicitly requires 30-day visibility. Editors need monthly cost impact visibility.
