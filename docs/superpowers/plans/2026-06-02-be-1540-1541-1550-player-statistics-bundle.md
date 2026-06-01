# BE bundle #1540 + #1541 + #1550 — PlayerStatistics computed fields

> **Status:** SHIPPED — implemented + tested + lint/typecheck/build pass.

**Goal:** Extend `PlayerStatistics` DTO + handler with three computed fields needed by the `/players/[id]` v2 surface, bundled in a single atomic BE PR for cycle efficiency:

| Issue | Field | Type |
|---|---|---|
| **#1540** | `LeaderboardRank` | `int?` — rank of current user among all users by completed wins (1-based); `null` when 0 sessions |
| **#1541** | `FavoriteAgentName` | `string?` — name of the most-used agent by chat-thread count; `null` when 0 threads w/ agent |
| **#1550** | `WinRateTrend` | `IReadOnlyList<MonthlyWinRate>` — last 6 ISO months `{Month: YYYY-MM, WinRate: 0..1}` |

All three fields are populated on the existing `GET /players/me/statistics` endpoint, mirror DTO shape in FE Zod schema (additive, optional during rollout).

**Architecture:** All three computations live in the existing `GetPlayerStatisticsQueryHandler` to reuse the loaded `records` collection and avoid extra DB round-trips. Two additional queries:

1. `LeaderboardRank`: SQL-side GroupBy on `PlayRecords` to count users with strictly more wins than the caller (predicate mirrors `PlayRecordOutcomeCalculator.HasWinner` as an EF-translatable lambda)
2. `FavoriteAgentName`: SQL-side GroupBy on `ChatThreads` (cross-context lookup via monolithic `MeepleAiDbContext` — physical single DbContext, logical cross-context coupling acknowledged)
3. `WinRateTrend`: in-memory aggregation over the already-loaded `records` list, sliding 6-month window from `DateTime.UtcNow` start of month

**Scale note (MVP):** `LeaderboardRank` uses per-user GroupBy over completed records — O(n) in record count. Acceptable for community scale < 100k users; revisit with a materialized leaderboard view when scale demands.

---

## Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| **DEC-1** | Bundle 3 BE expose-fields in 1 PR (vs 3 separate PRs) | Wiegers/Fowler — convergent pattern + 1 CI cycle vs 3 |
| **DEC-2** | LeaderboardRank predicate: STRICTLY MORE wins (not `>=`) — tied users share same rank | Cockburn — natural rank semantics; deterministic |
| **DEC-3** | LeaderboardRank returns `null` for 0-session users (unranked, not rank=N+1) | Wiegers — semantically meaningful "no participation" |
| **DEC-4** | FavoriteAgentName by chat-thread COUNT (not message count or duration) | Adzic — simplest criterion + matches issue body intent |
| **DEC-5** | FavoriteAgentName tie-breaker: deterministic `ORDER BY AgentId` after `Count DESC` | Wiegers — testable invariant |
| **DEC-6** | Cross-context query via monolithic `MeepleAiDbContext` (ChatThreads, AgentDefinitions) | Fowler — physical single DbContext, pragmatic for MVP; document as tech-debt to revisit if contexts ever split |
| **DEC-7** | WinRateTrend window: 6 ISO months sliding from start of current month UTC | Nygard — UTC-anchored, no DST/locale ambiguity |
| **DEC-8** | WinRateTrend excludes months with 0 plays; includes months with plays but 0 wins (renders 0%) | Adzic — empty bucket ≠ "valid 0%" datum (mirrors DEC-9 of #1546 PlayerTopGamesCard) |
| **DEC-9** | WinRateTrend ordered ASC by month string (StringComparer.Ordinal — ISO format sorts correctly) | Crispin — deterministic chronological order |
| **DEC-10** | FE Zod: 3 new fields `optional()` during BE rollout (mirrors #1663 Phase 2 pattern) | Fowler — additive non-breaking schema evolution |
| **DEC-11** | DTO record positional ctor: append 3 new params at the end (positional non-breaking for handler caller; only 1 caller `new PlayerStatisticsDto(...)`) | Wiegers — minimal blast radius |

## G/W/T Scenarios

**#1540 LeaderboardRank:**
- **L1**: G: user with 0 sessions → W: handle / T: `LeaderboardRank == null`
- **L2**: G: caller has 1 win, rival1 has 3 wins, rival2 has 2 wins → W: handle / T: `LeaderboardRank == 3`
- **L3**: G: caller has 5 wins, rival has 2 wins → W: handle / T: `LeaderboardRank == 1`

**#1541 FavoriteAgentName:**
- **F1**: G: user with 0 chat threads → W: handle / T: `FavoriteAgentName == null`
- **F2**: G: 2 threads agent A + 1 thread agent B → W: handle / T: `FavoriteAgentName == "Agent A name"`
- **F3**: G: threads with `AgentId == null` → W: handle / T: `FavoriteAgentName == null` (not picked from NULL group)

**#1550 WinRateTrend:**
- **W1**: G: records ALL older than 6 months → W: handle / T: `WinRateTrend.Should().BeEmpty()`
- **W2**: G: 2 records current month (1 win), 2 records prev month (2 wins) → W: handle / T: 2 buckets ordered ASC, current=0.5, prev=1.0
- **W3**: G: 1 record current month with 0 wins → W: handle / T: 1 bucket `WinRate == 0.0` (valid datum)

---

## File Structure

| Path | Responsibility | Type |
|------|---------------|------|
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayerStatisticsDto.cs` | Extend DTO with 3 new fields + add `MonthlyWinRate` record | Modify |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs` | Add `deriveLeaderboardRank` + `deriveFavoriteAgentName` + `deriveWinRateTrend` logic | Modify |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayerStatisticsQueryHandlerTests.cs` | Add 8 new tests (3 L + 3 F + 3 W − 1 deduplicated in empty default) + extend `MakePlayRecord` helper with `sessionDate` param | Modify |
| `apps/web/src/lib/api/schemas/play-records.schemas.ts` | Add `MonthlyWinRateSchema` + extend `PlayerStatisticsSchema` with 3 optional fields | Modify |
| `docs/superpowers/plans/2026-06-02-be-1540-1541-1550-player-statistics-bundle.md` | This plan doc | Create |

**Test commands:**
- `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetPlayerStatisticsQueryHandlerTests"`
- `cd apps/api && dotnet build src/Api/Api.csproj`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`

---

## Verification

- ✅ `dotnet test` PlayerStatisticsHandler → **15/15 pass** (1s)
- ✅ `dotnet build` BE → 0 errors, 0 warnings (after 3 analyzer fixes: S1155 + CA1827 + MA0002)
- ✅ `pnpm typecheck` FE → 0 errors
- ✅ `pnpm lint` FE → 0 errors (6 pre-existing warnings on unrelated files)

## Acceptance criteria

- [x] #1540: `PlayerStatistics.LeaderboardRank` exposed; `null` for 0-session users
- [x] #1541: `PlayerStatistics.FavoriteAgentName` exposed via cross-context ChatThread aggregation
- [x] #1550: `PlayerStatistics.WinRateTrend` exposed as `Array<{month, winRate}>` for last 6 ISO months
- [x] FE Zod `PlayerStatisticsSchema` extended additively (optional during rollout)
- [x] All 3 fields populate `PlayerOverviewRegion` sub-cards rendering real data (FavoriteAgentCard, PlayerLeaderboardCard) instead of `"none"`/`"noRank"` fallbacks
- [x] #1549 PlayerTrendCard (Step E next) unblocked by `WinRateTrend` field

## Out of scope

- FE wire-up of new fields in `PlayerDetailView.mapStatsToProfile` (already wired — current mapper passes `leaderboardRank` and `favoriteAgentName` through; will now render real data once BE deploys)
- PlayerTrendCard FE component (Step E #1549, separate PR)
- Refactor LeaderboardRank to materialized view (deferred — MVP scale OK)

## Cluster #1485 player-detail follow-up status

- ✅ #1546 (PlayerTopGamesCard) — sessione 22
- ✅ #1547 (a11y ErrorShell+NotFoundShell) — Step B
- ✅ **#1540 + #1541 + #1550 (BE bundle)** — this PR (Step C)
- 🟡 #1542 (player achievements BE+FE) — Step D pending
- 🟡 #1549 (PlayerTrendCard FE Tier L) — Step E pending (unblocked by this PR)
