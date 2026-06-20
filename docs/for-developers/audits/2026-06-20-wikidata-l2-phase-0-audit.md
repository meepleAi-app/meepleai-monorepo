---
title: Wikidata L2 Phase G — Phase 0 Audit
issue: #2055
date: 2026-06-20
status: GREEN — proceed Phase 1+
author: Claude Opus 4.7
---

# Phase 0 Audit — Wikidata L2 Phase G

## Decision: 🟢 GREEN

All 3 critical gaps resolved pre-audit or verified as-shipped. No new BLOCKING gap discovered. Proceed Phase 1+.

## Audit checklist

### Task 0.1: Aggregate file + required fields

✅ **PASS** — `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs` exists with all required fields:

| Field | Status |
|-------|--------|
| `RetryCount` | ✅ Present |
| `NextRetryAt` | ✅ Present |
| `DeadLetteredAt` | ✅ Present |
| `AcknowledgedAt` | ✅ Present |
| `AcknowledgedBy` | ✅ Present |
| `TriggeredByAdminUserId` | ✅ Present |

DEC-3l satisfied: `RetryCount` + `Details` sufficient; NO new fields (`ScheduledAttemptCount`, `LastError`) needed.

DEC-3l-1 confirmed: no circuit breaker state column in table (runtime Polly only).

### Task 0.2: Migrations exist

✅ **PASS** — 3 migrations present:
- `20260610094431_AddWikidataQidColumnsToSharedGames.cs`
- `20260610110434_AddWikidataCoverEnrichmentAttemptsTable.cs`
- `20260613175905_AddAcknowledgeAndTriggerSourceToWikidataAttempts.cs`

No new migration needed for Phase G.

### Task 0.3: M15 quarterly job

✅ **PASS** — File exists: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataQuarterlyReVerificationJob.cs`

⚠️ **Phase 2 follow-up**: test coverage to audit (count + scenarios). Acceptance criterion AC-G1 requires ≥ 5 test coverage + metric integrated. Verify in Phase 2.

### Task 0.4: Cache-Control header (DEC-3h)

✅ **PASS / DESCOPED** — Audit immediato 2026-06-20 ha confermato `CoverR2UploadPipeline.cs:18,67`:

```csharp
private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
// ...
request.Headers.CacheControl = ImmutableCacheControl;
```

Phase 4 **DESCOPED**. AC-G3 closed as-shipped.

### Task 0.5: ImageSharp license (DEC-3d-1)

🚨 **CONFIRMED CROSS-CUTTING REFACTOR REQUIRED** — `apps/api/src/Api/Api.csproj:115` pins `SixLabors.ImageSharp Version="3.1.12"`.

ImageSharp 3.x = Six Labors Split License (commercial paid). MeepleAI is proprietary → incompatible.

Files affected (cross-cutting, NOT just #2055):
- `apps/api/src/Api/Api.csproj:115` (and `apps/api/tests/Api.Tests/Api.Tests.csproj:50`)
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/WebpVariantGenerator.cs` (Phase F shipped)
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/SessionAttachmentService.cs` (pre-existing)
- Tests for both

Phase 3 expanded ~0.5gg → ~1.5gg per DEC-3d-1 LOCKED (Magick.NET-Q8-AnyCPU 14.x Apache 2.0 migration).

### Task 0.6: Grafana dashboard wikidata existence

⚠️ **CONFIRMED PHASE 5 NEEDED** — `infra/monitoring/grafana/dashboards/` contains:
- `epic-4068-complete-dashboard.json`
- `k6-load-testing.json`
- `llm-operational-maturity.json`
- `multi-tier-cache-performance.json`
- `provider-quota.json`
- `shared-catalog-performance.json`
- `shared-games.json`

No dedicated `wikidata-enrichment.json`. Phase 5 (AC-G4) scope confirmed: create new dashboard.

### Task 0.7: FE wikidataCover* types existence

⚠️ **CONFIRMED PHASE 7 NEEDED** — `grep wikidataCover` in `apps/web/src` returns 0 matches. BE shipped `wikidata_cover_*` fields via Phase F, FE TS types + consumer wiring deferred to Phase G7 (AC-G6).

Phase 7 (FE attribution footer) scope confirmed.

## Summary

| Phase | Status | Effort |
|-------|--------|--------|
| Phase 0 audit | ✅ GREEN COMPLETE | ~0.5gg |
| Phase 1 ADR-082 confirm | Pending | ~0.5gg |
| Phase 2 M15 job harden | Pending (file exists) | ~0.5gg |
| **Phase 3 Magick.NET migration** | Pending (cross-cutting!) | ~1.5gg |
| ~~Phase 4 Cache-Control~~ | ✅ DESCOPED already-shipped | 0gg |
| Phase 5 Grafana panel | Pending (no dashboard exists) | ~0.5gg |
| Phase 6 WireMock contract tests | Pending | ~1gg |
| Phase 7 FE attribution footer | Pending (no FE wiring) | ~0.5gg |
| Phase 8 Staging dry-run + PR | Pending | ~0.5gg |
| **Total residual** | — | **~5gg** (vs ~5-5.5gg plan estimate) |

## Decision

**GREEN** — proceed Phase 1+. ImageSharp Magick.NET migration is cross-cutting but
not blocking; refactor lives in Phase 3 with full test re-run validation.

Plan path: [`docs/superpowers/plans/2026-06-20-wikidata-l2.md`](../../superpowers/plans/2026-06-20-wikidata-l2.md)
