# FE #1542 — player achievements hook + PlayerDetailView wire

> **Status:** SHIPPED — implemented + tested + lint/typecheck pass.

**Goal:** Wire the existing `GET /api/v1/achievements` endpoint (already implemented by `Gamification.GetAchievementsQueryHandler`, Issue #3922) into `PlayerDetailView.mapStatsToProfile` so `achievementCount` reflects the real unlock count instead of the legacy hardcoded `0`. Closes #1542.

**P74 discovery (sessione 23, Step D):** the issue body described a missing BE endpoint + missing FE Zod/hook, but a fresh code audit revealed the BE is already 100% shipped via the **`Gamification`** bounded context (full repository pattern + cache + `AchievementRuleEvaluator` + `AchievementEvaluationJob` background job). The FE already consumed it via `AchievementsGrid.tsx` with an inline `useQuery`. Step D scope therefore reduces to extracting a shared hook and wiring it into the player overview.

---

## Architecture

- **Hook extraction (DRY):** `apps/web/src/hooks/queries/useAchievements.ts` exports `useAchievements()` and re-exports `AchievementDto`. `AchievementsGrid.tsx` refactored to consume the shared hook (no behavioral change; same query key `['achievements']`, same staleTime 5min).
- **PlayerDetailView wire (#1542 core):** `PlayerDetailView` calls both `usePlayerStatistics()` and `useAchievements()` in parallel; the `profile` `useMemo` derives `achievementCount = data?.filter(a => a.isUnlocked).length ?? 0`. Graceful degrade when achievements query is loading/errored (count stays 0).
- **No new types/schemas:** the `AchievementDto` interface remains an inline TS interface (no Zod) — matches the pre-existing `AchievementsGrid` pattern; tighten with Zod when contract diverges.

---

## Locked Decisions

| # | Decision | Rationale |
|---|---|---|
| **DEC-1** | Reuse `Gamification.GetAchievementsQueryHandler` via existing `/api/v1/achievements` endpoint instead of creating a new `/players/me/achievements` endpoint | Wiegers / Fowler — DRY, single source of achievement data, no BE work |
| **DEC-2** | Extract `useAchievements` hook to `apps/web/src/hooks/queries/` and refactor `AchievementsGrid` to consume it (vs duplicate hook with shared `AchievementDto`) | Fowler — single source of truth, smaller refactor than parallel hook+type duplicates |
| **DEC-3** | Re-export `AchievementDto` from `AchievementsGrid.tsx` to preserve backward compatibility for downstream consumers (e.g. `lib/sessions-summary/fsm.ts` imports the type from this module) | Cockburn — minimize blast radius, no consumer-side breaking change |
| **DEC-4** | Skip Zod schema parsing for now (mirror pre-existing inline interface pattern) | Adzic — minimal change; documented as MINOR tech-debt; revisit if contract diverges |
| **DEC-5** | Graceful degrade `achievementCount = 0` when `useAchievements` is loading/errored (vs blocking render until achievements ready) | Wiegers — UX consistency with existing null-fallback fields like `leaderboardRank` |
| **DEC-6** | Use `a.isUnlocked` (not progress > 0) as the unlock criterion for the count | Adzic — matches `Gamification.UserAchievement.IsUnlocked` semantics |
| **DEC-7** | Profile `useMemo` dependency adds `achievementsQuery.data` (re-derive on achievements load) | Crispin — reactivity correctness, no manual invalidation needed |

## G/W/T Scenarios

- **A1**: G: user has 3 unlocked achievements + 2 locked → W: hook returns 5 items, PlayerDetailView derives count / T: `achievementCount === 3`
- **A2**: G: useAchievements still loading (`data === undefined`) → W: PlayerDetailView renders / T: `achievementCount === 0` (graceful)
- **A3**: G: useAchievements errored (`error != null`) → W: PlayerDetailView renders / T: `achievementCount === 0` (graceful)
- **A4**: G: AchievementsGrid mounted → W: refactor to useAchievements hook / T: same render output as pre-refactor (no behavioral regression)
- **A5**: G: API returns `null` → W: hook normalizes to `[]` (defensive) / T: `achievementCount === 0`
- **A6**: G: hook called from any consumer → W: `apiClient.get('/api/v1/achievements')` invoked / T: assertion on mock

---

## File Structure

| Path | Responsibility | Type |
|------|---------------|------|
| `apps/web/src/hooks/queries/useAchievements.ts` | Shared hook + `AchievementDto` interface | Create |
| `apps/web/src/hooks/queries/__tests__/useAchievements.test.tsx` | 3 hook unit tests (success + null normalize + error) | Create |
| `apps/web/src/components/profile/AchievementsGrid.tsx` | Refactor to consume shared hook; re-export `AchievementDto` for backcompat | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx` | Add `useAchievements()` call + derive `achievementCount` in `profile` useMemo | Modify |
| `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx` | Add `vi.mock` for `useAchievements` + default `mockAchievementsQuery` returning `{data: []}` | Modify |
| `docs/superpowers/plans/2026-06-02-fe-1542-player-achievements-hook.md` | This plan doc | Create |

**Test commands:**
- `cd apps/web && pnpm vitest run "src/hooks/queries/__tests__/useAchievements.test.tsx"`
- `cd apps/web && pnpm vitest run "src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx"`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`

---

## Verification

- ✅ `pnpm vitest run` useAchievements.test → **3/3 pass** (227ms)
- ✅ `pnpm vitest run` PlayerDetailView.test → **24/24 pass** (337ms) — no regression after `useAchievements` mock added
- ✅ `pnpm typecheck` → 0 errors
- ✅ `pnpm lint` → 0 errors (6 pre-existing warnings on unrelated files)

## Acceptance criteria

- [x] FE Zod schema / hook ship with `useQuery` + cache + tests *(hook ✅; Zod skipped per DEC-4)*
- [x] Orchestrator wires the achievement count from real data
- [x] No regression in `AchievementBadgeGrid` (it stays compatible with `count` prop — the orchestrator passes `safeProfile.achievementCount` unchanged)

## Out of scope

- Zod schema for `AchievementDto` (DEC-4 — deferred MINOR tech-debt)
- New `/players/me/achievements` endpoint (DEC-1 — superseded by existing `/api/v1/achievements`)
- Refactor `AchievementBadgeGrid` placeholder → real achievement icon grid (post-MVP per #1542 body)

## Cluster #1485 player-detail follow-up status

- ✅ #1546 PlayerTopGamesCard (sessione 22)
- ✅ #1547 a11y ErrorShell+NotFoundShell (Step B PR #1796)
- ✅ #1540 + #1541 + #1550 BE bundle (Step C PR #1797)
- ✅ **#1542 player achievements hook + wire (this PR, Step D)**
- 🟡 #1549 PlayerTrendCard FE Tier L — Step E next (unblocked by Step C `WinRateTrend`)
