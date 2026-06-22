# #2438 — Play Records Stats: trend chart + date-range + Redis cache — Design

**Status**: APPROVED (design lock 2026-06-20)
**Issue**: #2438 (follow-up of #2350 / epic #2346)
**Parent**: US-INT-2 Tier 2 Play Records

## Context & rescope

Discovery (2026-06-20) found the issue body over-estimates the work — most BE is already shipped:

- `PlayerStatisticsDto.WinRateTrend` (`IReadOnlyList<MonthlyWinRate>`, `{Month: "YYYY-MM", WinRate: 0..1}`) is **already populated** (6-month sliding window) in `GetPlayerStatisticsQueryHandler` (lines 168-198).
- The query handler **already filters** `StartDate`/`EndDate` (lines 41-49). The FE just never sends them (`playRecordsApi.getPlayerStatistics()` takes no params).
- `LeaderboardRank` scalar is already exposed; a leaderboard *board* (list) would be a NEW query — **out of scope** (not selected).
- The canonical mockup `sp4-play-records-stats` is "4 KPI + 2 bar" and the current FE `StatisticsView` matches it exactly. All features below go **beyond** the mockup → designer self-waiver (P250, user is the designer; fidelity.json already `design_intent: current`, self-approved 2026-06-15).

## Scope (locked via AskUserQuestion 2026-06-20)

**In scope** — 3 pieces, 2 PRs (FE-first split):

| # | Piece | Layer | PR |
|---|---|---|---|
| 1 | Trend chart (recharts area, consumes `winRateTrend`) | FE | PR-A |
| 2 | Date-range filter (preset segmented control) | FE | PR-A |
| 3 | Redis server cache + invalidation | BE | PR-B |

**Out of scope** (not selected — YAGNI): CSV export, leaderboard board. They remain unchecked in the #2438 body.

## PR-A — Frontend

### Component 1: `TrendChart`

- **File**: `apps/web/src/components/play-records/stats/TrendChart.tsx`
- **Input**: `stats: PlayerStatistics` (uses `stats.winRateTrend`).
- **Render**: recharts `AreaChart` — X axis = `month` (formatted `MMM` short), Y axis = win rate scaled to 0–100%, `Tooltip` showing month + `NN%`. Single `Area` series, semantic-token stroke/fill (`var(--primary)` via Tailwind `text-primary`/arbitrary value — no raw HSL).
- **States**: `default` (≥1 month) · `empty` (`winRateTrend.length === 0` → muted "dati insufficienti" panel, no chart).
- **a11y**: chart wrapped with `role="img"` + `aria-label` summarizing the trend; a visually-hidden `<table>` mirrors the data points for screen readers (axe AA, 0 violations).
- **Placement**: rendered in `StatisticsView` as a full-width section **below** the 2-col `MostPlayedBar`/`WinByGameBar` grid.

### Component 2: Date-range filter

- **API**: extend `playRecordsApi.getPlayerStatistics(params?: { dateFrom?: string; dateTo?: string })` → append `dateFrom`/`dateTo` query params when present. (Backend `GetStatisticsQueryParams` already binds `StartDate`/`EndDate`; endpoint maps them — verify query-param names `startDate`/`endDate` vs `dateFrom`/`dateTo` during planning and align.)
- **Hook**: `usePlayerStatistics(range?: StatsRange)` — `range` participates in the React Query key so each range is cached separately.
- **UI**: a **preset segmented control** in the `StatisticsView` header: `Tutto · 30g · 90g · 12 mesi`. Selecting a preset computes `dateFrom` client-side (`now - N`) and re-queries; `Tutto` sends no params. Preset chosen over a custom date-picker (YAGNI — covers the real need without a calendar widget).
- **State**: selected preset held in `StatisticsView` local state (`useState`), default `Tutto`.

## PR-B — Backend

### Component 3: Redis cache on `GetPlayerStatisticsQueryHandler`

- **Cache**: wrap the handler body with `IHybridCacheService` (pattern from `GetGameLeaderboardQueryHandler`), **TTL 5 min**.
- **Key**: `player-stats:{userId}:{startDate?:o}:{endDate?:o}` (date parts use round-trip `o` format or empty when null).
- **Invalidation**: a new `INotificationHandler<>` reacting to `PlayRecordCreatedEvent`, `PlayRecordCompletedEvent`, and `PlayRecordDeletedEvent` (introduced in #2439) → evicts the user's stats cache entries. Because keys are date-parametrized, eviction uses a **per-user tag** (`IHybridCacheService` tag-based eviction) rather than enumerating date combinations — verify tag support during planning; fallback = cache a single un-parametrized `player-stats:{userId}` entry and apply date filters post-cache (simpler, still correct) if tags are unavailable.
- **ADR**: a short ADR documents the key schema + the invalidation trigger set + the tag-vs-single-entry decision.

## Testing

- **PR-A**: `TrendChart` unit (default render asserts data points; empty state) + axe AA test; date-range hook+UI unit (preset change re-queries with correct `dateFrom`); no regression in existing `StatisticsView` tests.
- **PR-B**: handler cache unit (miss → compute+store; hit → no DB call); invalidation handler unit (each of the 3 events evicts); integration (Testcontainers) end-to-end cache+invalidation.

## Risks

- **R1**: query-param naming mismatch FE↔BE (`dateFrom/dateTo` vs `startDate/endDate`) — resolved in planning by reading the endpoint binding.
- **R2**: `IHybridCacheService` tag-based eviction may not exist → fallback to single un-parametrized cache entry (documented in ADR).
- **R3**: recharts SSR/`'use client'` — `TrendChart` is a client component; ensure dynamic import is not needed (recharts works in client components).

## Self-review

- **Placeholders**: none — every piece has a concrete file/approach. The two "verify during planning" notes (R1 param names, R2 tag support) are explicit risk-resolutions with named fallbacks, not vague TODOs.
- **Consistency**: scope table (3 pieces) matches the per-PR sections. CSV/leaderboard consistently marked out-of-scope.
- **Scope**: focused; FE-first split keeps PR-A independently shippable (consumes already-shipped BE data). PR-B is independently shippable (pure server-side optimization, no FE change).
- **Ambiguity**: date-range = preset (not date-picker) — made explicit.
