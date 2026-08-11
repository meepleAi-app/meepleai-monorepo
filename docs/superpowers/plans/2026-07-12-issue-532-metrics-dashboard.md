# #532 ME-M2.3 Metrics Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use `- [ ]`. This plan mirrors mapped precedents — each task points to the exact precedent file:line to copy the shape from (spec has the locked contract).

**Goal:** Full admin metrics dashboard for Mechanic Extractor (cost / review-time / approval-rate KPIs, rejection breakdown, daily-cost time-series, filterable+paginated recent-analyses table, CSV export) — all 6 ACs, BE + FE.

**Spec (locked contract):** `docs/superpowers/specs/2026-07-12-issue-532-metrics-dashboard.md`

## Global Constraints
- CQRS `IQuery<T>`/`IQueryHandler<T,R>`; metrics query handlers inject `MeepleAiDbContext`, query `MechanicAnalyses.AsNoTracking()` (analytics precedent — NOT a repo). Query filter already excludes `IsSuppressed`.
- BE endpoints on the existing group `app.MapGroup("/admin/mechanic-analyses").AddEndpointFilter<RequireAdminSessionFilter>()` (`Routing/AdminMechanicAnalysesEndpoints.cs:32`) — no per-endpoint auth needed.
- Status ints: Draft0/InReview1/Published2/Rejected3/PartiallyExtracted4. Approved=2; Rejected∈{3,4}. Approval rate over reviewed (`ReviewedAt != null`).
- FE route `/admin/knowledge-base/mechanic-extractor/metrics` (sibling-consistent). recharts via `components/admin/AdminCharts.tsx`; table via `EntityTableView`; KPI tiles mirror `components/admin/agents/MetricsKpiCards.tsx`.
- Backend tests `apps/api/tests/Api.Tests` (kill testhost first). FE tests Vitest.

## BE precedents to mirror (exact)
- KPI aggregation: `BoundedContexts/Administration/Application/Queries/GetAiRequestStatsQueryHandler.cs` (+ `AiRequestStats` DTO).
- Daily time-series + gap-fill: `.../GetApiRequestsByDayQueryHandler.cs` (+ `ApiRequestByDayDto`).
- Filtered+paginated: `.../GetAiRequestsQueryHandler.cs` (+ `AiRequestListResult`).
- CSV export: `.../ExportAuditLogsQueryHandler.cs` + `Routing/AdminAuditLogEndpoints.cs:48-72` (`Results.File`).
- Endpoint reg: `Routing/AnalyticsEndpoints.cs` (query-param binding).

---

### Task 1: BE — DTOs + summary query (KPIs + rejection breakdown)
**Files:** Create `BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicMetricsDtos.cs` (all 5 DTOs from spec); `Application/Queries/MechanicMetrics/GetMechanicMetricsSummaryQuery.cs` + `...Handler.cs`. Test: `tests/.../SharedGameCatalog/Infrastructure/MechanicMetricsSummaryQueryTests.cs`.
- [ ] Write failing integration test: seed a game + analyses (2 Published w/ cost 1.0+3.0, 1 Rejected reason "factual", 1 InReview) → send `GetMechanicMetricsSummaryQuery(null,null,null,null)` via IMediator → assert `TotalCostUsd`, `PublishedCount=2`, `RejectedCount=1`, `AverageCostUsd`, `ApprovalRatePct` (2/3), `RejectionBreakdown` contains ("factual",1). Reuse `MechanicCardAutoSuppressionSeed`-style seeding (SharedGame + MechanicAnalysisEntity with the fields).
- [ ] Run → FAIL (types missing).
- [ ] Implement DTOs + handler (mirror `GetAiRequestStatsQueryHandler`: `MechanicAnalyses.AsNoTracking()` + optional `.Where` filters (gameId/reviewerId/startDate/endDate on CreatedAt) → materialize to a lightweight projection → compute counts/avg/approval/rejection-breakdown in memory or via grouped queries). `AverageReviewTimeHours` = avg of `(ReviewedAt-CreatedAt).TotalHours` where ReviewedAt != null (null if none).
- [ ] Run → PASS. Commit `feat(mechanic-extractor): #532 BE metrics summary query + DTOs`.

### Task 2: BE — cost-by-day time-series + recent paginated list
**Files:** `Application/Queries/MechanicMetrics/GetMechanicCostByDayQuery.cs`+Handler; `GetMechanicRecentAnalysesQuery.cs`+Handler. Tests: `MechanicCostByDayQueryTests.cs`, `MechanicRecentAnalysesQueryTests.cs`.
- [ ] cost-by-day: failing test (2 analyses today, 1 three days ago; `days=7`) → 7 buckets, correct sums, gap-filled zeros. Implement mirroring `GetApiRequestsByDayQueryHandler` (`GroupBy(a => a.CreatedAt.Date)` → `{Date, Sum(EstimatedCostUsd), Count}` → gap-fill loop over `days`). Optional gameId/reviewerId filters.
- [ ] recent: failing test (seed 3 analyses across 2 games/statuses) → filter by gameId returns only that game; by status filters; pagination `limit/offset` + `TotalCount`; rows carry GameName (join SharedGame) + ReviewerName (LEFT join users on ReviewedBy). Implement mirroring `GetAiRequestsQueryHandler` (filter chain + `CountAsync` + `OrderByDescending(CreatedAt).Skip/Take`). Map to `MechanicRecentAnalysisRowDto`.
- [ ] Run both → PASS. Commit `feat(mechanic-extractor): #532 BE cost-by-day + recent analyses queries`.

### Task 3: BE — CSV export + endpoint registration
**Files:** `Application/Queries/MechanicMetrics/ExportMechanicAnalysesQuery.cs`+Handler (+ `ExportMechanicAnalysesResult(byte[] Content, string ContentType, string FileName)`); modify `Routing/AdminMechanicAnalysesEndpoints.cs` (4 new `group.MapGet` under `/metrics/*`). Tests: `ExportMechanicAnalysesQueryTests.cs` + endpoint integration for one path.
- [ ] export: failing test → CSV bytes contain header `Id,GameName,Status,ReviewerId,CreatedAt,ReviewedAt,EstimatedCostUsd` + one row per analysis, proper escaping (mirror `ExportAuditLogsQueryHandler` `EscapeCsv`), cap 10k. `ContentType="text/csv"`, `FileName="mechanic-analyses-{yyyyMMdd-HHmmss}.csv"`.
- [ ] endpoints: add `GET /metrics/summary`, `/metrics/cost-by-day`, `/metrics/recent`, `/metrics/export` handlers to the group (query-param binding like `AnalyticsEndpoints`; export returns `Results.File(r.Content, r.ContentType, r.FileName)`). Integration test hits `GET /api/v1/admin/mechanic-analyses/metrics/summary` (admin session via `TestSessionHelper.CreateAdminSessionAsync`) → 200 + shape.
- [ ] `dotnet build` + run BE #532 suite → PASS. Commit `feat(mechanic-extractor): #532 BE CSV export + metrics endpoints`.

### Task 4: FE — client + zod schemas
**Files:** `apps/web/src/lib/api/schemas/admin-mechanic-metrics.schemas.ts` (zod mirror of the 5 DTOs); `apps/web/src/lib/api/clients/admin/adminMechanicMetricsClient.ts` (`getSummary/getCostByDay/getRecent(params)` + `exportCsv(params): Promise<Blob>`); compose into `adminClient.ts`. Test: `__tests__` schema parse + client (mock http).
- [ ] Failing test: schema parses a sample summary/cost-by-day/recent payload; client `getSummary` calls the right URL (mock HttpClient). Implement mirroring `adminAnalyticsClient.ts` (`http.get(url, Schema)`) + `exportLedgerEntries` blob pattern for `exportCsv`.
- [ ] Run → PASS. `pnpm typecheck`. Commit `feat(mechanic-extractor): #532 FE metrics client + schemas`.

### Task 5: FE — dashboard page (KPI + chart + table + filters + CSV)
**Files:** `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/metrics/page.tsx` + supporting components under `components/admin/mechanic-extractor/metrics/` (KpiCards, CostChart, RecentTable with filters). Tests: `__tests__/page.test.tsx`.
- [ ] Failing test: render page (mock client via `vi.mock`), assert KPI tiles + chart container + table render; changing a filter dropdown updates the query (assert client called with the filter); date-range toggle 7/30/90 re-queries cost-by-day; CSV button triggers `exportCsv`. Mock recharts if needed (jsdom).
- [ ] Implement: `'use client'` page with `useQuery` per endpoint keyed on `{period,gameId,reviewerId,status,offset}`; KPI tiles (mirror `MetricsKpiCards`); `AdminCharts` BarChart for cost-by-day; `EntityTableView` for recent + 3 filter dropdowns (game options from a `getRecent` distinct or a small `/metrics/filters` — simplest: derive game/reviewer options from the recent result's rows, or add a lightweight distinct query; status is a fixed enum list); Export CSV button → `exportCsv` → blob download (mirror `downloadFile` util `lib/utils/export.ts`).
- [ ] `pnpm typecheck` + `pnpm lint` + `pnpm exec vitest run <files>` → PASS. Commit `feat(mechanic-extractor): #532 FE metrics dashboard page`.

### Task 6: Nav + wire-up + final
- [ ] Add a nav entry "Mechanic Metrics" → `/admin/knowledge-base/mechanic-extractor/metrics` in `admin-nav-config.ts` (group D, near "Mechanic Analyses").
- [ ] Full `dotnet build` + BE suite + FE typecheck/lint/vitest green.
- [ ] Commit `feat(mechanic-extractor): #532 nav entry + wire-up`.

## Self-Review
Covers all 6 ACs: route (Task 5) · KPIs+rejection breakdown (Task 1) · cost time-series (Task 2) · recent table (Task 2) · filters (Task 2 BE params + Task 5 UI) · CSV export (Task 3 BE + Task 5 button). Filter data-source for reviewer/game dropdowns: derive from recent rows or a light distinct — decide in Task 5 (simplest that avoids an extra heavy query).
