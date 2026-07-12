# #532 — ME-M2.3 Admin metrics dashboard (spec)

**Parent ADR**: ADR-051 · **Depends on**: #527 · **BC**: SharedGameCatalog (BE) + admin FE · **Date**: 2026-07-12
**Scope scelto (utente)**: Full 6 AC in un PR (BE completo + FE completa incl. UI filtri).

## Obiettivo
Dashboard admin operativa per costi + qualità della pipeline Mechanic Extractor: KPI, breakdown rejection,
time-series costo giornaliero, tabella analisi recenti filtrabile, export CSV.

## Dati (verificati)
`MechanicAnalysisEntity` (DbSet `MechanicAnalyses`, query filter `!IsSuppressed`): `EstimatedCostUsd` (decimal),
`TotalTokensUsed` (int), `Status` (int: Draft0/InReview1/Published2/Rejected3/PartiallyExtracted4), `SharedGameId`,
`CreatedAt`, `ReviewedBy` (Guid?), `ReviewedAt` (DateTime?, set su approve+reject+auto+partial), `RejectionReason` (string?).
- **Approval rate** = Published / (Published + Rejected + PartiallyExtracted), su reviewed (ReviewedAt != null).
- **Review time** = ReviewedAt − CreatedAt (solo reviewed).
- **Rejection breakdown** = GROUP BY RejectionReason dove Status ∈ {3,4}.
Le query metrics interrogano `MeepleAiDbContext.MechanicAnalyses` direttamente `.AsNoTracking()` (pattern analytics
Administration, NO repo). CQRS `IQuery<T>`/`IQueryHandler<T,R>`.

## Route (riconciliazione)
AC dice `/admin/mechanic-extractor/metrics`; i sibling stanno sotto `/admin/knowledge-base/mechanic-extractor/`.
→ **FE**: `/admin/knowledge-base/mechanic-extractor/metrics` (coerenza sibling + già target del deep-link #535).
→ **BE**: sub-path del group esistente `/admin/mechanic-analyses` (`AdminMechanicAnalysesEndpoints`, filter `RequireAdminSessionFilter`).

## API contract (LOCKED)

### DTOs (`Application/DTOs/MechanicMetricsDtos.cs`)
```csharp
internal sealed record MechanicMetricsSummaryDto(
    decimal TotalCostUsd, int TotalAnalyses, int PublishedCount, int RejectedCount, int InReviewCount,
    decimal AverageCostUsd, double? AverageReviewTimeHours, double ApprovalRatePct,
    IReadOnlyList<RejectionReasonCountDto> RejectionBreakdown);
internal sealed record RejectionReasonCountDto(string Reason, int Count);
internal sealed record MechanicCostByDayDto(DateOnly Date, decimal CostUsd, int AnalysisCount);
internal sealed record MechanicRecentAnalysisRowDto(
    Guid Id, Guid SharedGameId, string GameName, int Status, Guid? ReviewedBy, string? ReviewerName,
    DateTime CreatedAt, DateTime? ReviewedAt, decimal EstimatedCostUsd);
internal sealed record MechanicRecentAnalysesResult(IReadOnlyList<MechanicRecentAnalysisRowDto> Items, int TotalCount);
```

### Endpoints (group `/admin/mechanic-analyses`)
| Verb/Path | Query params | Response |
|---|---|---|
| `GET /metrics/summary` | `gameId?`, `reviewerId?`, `startDate?`, `endDate?` | `MechanicMetricsSummaryDto` |
| `GET /metrics/cost-by-day` | `days`=7\|30\|90 (default 30), `gameId?`, `reviewerId?` | `MechanicCostByDayDto[]` (gap-filled) |
| `GET /metrics/recent` | `limit`=25, `offset`=0, `gameId?`, `reviewerId?`, `status?` | `MechanicRecentAnalysesResult` |
| `GET /metrics/export` | `gameId?`, `reviewerId?`, `status?`, `startDate?`, `endDate?` | CSV `Results.File` (`mechanic-analyses-{yyyyMMdd-HHmmss}.csv`) |

Query handlers: `GetMechanicMetricsSummaryQuery(...)`, `GetMechanicCostByDayQuery(...)`, `GetMechanicRecentAnalysesQuery(...)`,
`ExportMechanicAnalysesQuery(...)`. Summary usa `GroupBy(_ => 1)` + `Count(predicate)`/`Average`/`Sum` (pattern
`GetAiRequestStatsQueryHandler`); cost-by-day `GroupBy(CreatedAt.Date)` + gap-fill loop (pattern `GetApiRequestsByDayQueryHandler`);
recent = filtered + `Skip/Take` + `CountAsync` (pattern `GetAiRequestsQueryHandler`) + join SharedGame (GameName) + LEFT join users (ReviewerName);
export = StringBuilder CSV + `ExportMechanicAnalysesResult(byte[] Content, string ContentType, string FileName)` (pattern `ExportAuditLogsQueryHandler`, cap 10k righe).

## FE (`apps/web`)
- **Client**: nuovo `createAdminMechanicMetricsClient(http)` (o metodi su `adminClient`) → `getSummary/getCostByDay/getRecent(params)` + `exportCsv(params): Promise<Blob>` (fetch().blob(), pattern `exportLedgerEntries`). Zod schemas mirror dei DTO.
- **Page** `app/admin/(dashboard)/knowledge-base/mechanic-extractor/metrics/page.tsx` (`'use client'`):
  - Date-range toggle 7/30/90d (pattern agent analytics).
  - KPI tiles (pattern `MetricsKpiCards`, lucide DollarSign/Clock/CheckCircle): avg cost, avg review time, approval rate, total analyses.
  - Rejection breakdown (bar o lista).
  - Cost-by-day chart (recharts `BarChart`/`LineChart` via `AdminCharts`).
  - Recent-analyses table (`EntityTableView`) con paginazione + **filtri UI**: dropdown game (data-source: giochi con analisi), dropdown reviewer (reviewer distinti), dropdown status. I filtri ripilotano `useQuery` via queryKey.
  - Bottone **Export CSV** → download blob.
- React Query `useQuery` keyed su `{period, gameId, reviewerId, status, offset}`.

## Test (TDD)
- **BE** (Testcontainers): summary KPI (cost avg, approval rate, rejection breakdown) su set seedato (published/rejected/inreview mix); cost-by-day gap-fill + somma per giorno; recent filtered+paginated (per game/reviewer/status) + TotalCount; export CSV header + row count + escaping. + query handler unit dove sensato.
- **FE** (Vitest): page render KPI + chart + table (mock client); filtri cambiano queryKey; export button chiama client. Schemas parse.

## Fuori scope
- Nessuno (full 6 AC). Filtri per-reviewer data-source = reviewer distinti dalle analisi (no admin user list separata).
- Certification/quality metrics (M2 `MechanicAnalysisMetrics`) restano fuori (#532 = cost/velocity).
