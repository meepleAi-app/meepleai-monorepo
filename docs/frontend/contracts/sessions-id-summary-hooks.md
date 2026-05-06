# `/sessions/[id]` Summary Hook Contract — Wave D.3 Phase 0.5

> **Phase 0.5 contract** per V2 migration spec sezione 5.
> **Tier**: M-L (multi-section vertical scroll + 4 hook composition + reduced-motion confetti + tied podium logic + ShareCard preview boundary).
> **Scope**: orchestrator `SessionSummaryView` per route `/sessions/[id]` (post-game summary).
> **Issue**: TBD (child di umbrella #582). **Mockup**: `admin-mockups/design_files/sp4-session-summary.{html,jsx}` + `sp4-session-summary-parts.{html,jsx}`.
>
> Pattern blueprint da Wave C.1 ([games-id-hooks.md](games-id-hooks.md)) e Wave D.2 ([sessions-id-live-hooks.md](sessions-id-live-hooks.md)) adattato per **post-completed detail page con 4-hook composition + tie-group computation + accessibility-aware confetti + ShareCard preview-only scope**.
>
> **Audit gates applicati** (post-D.1 amendments PR #741): A (ICU plural) · B (schema reality v1 carryover) · C (MeepleCard API-fit) · D (bootstrap-then-merge). Vedi `docs/superpowers/specs/2026-05-05-wave-d-spec-panel-review.md` §3.3 + §10.

## 1. Overview

| Aspect | Value |
|--------|-------|
| Route path | `/sessions/[id]` (currently rendering live UI — D.3 = brownfield FORK) |
| Page component | `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx` |
| Orchestrator | `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` |
| Param source | `useParams<{ id: string }>()` |
| **Tier** | **M-L** (4-hook composition + visual flourishes + tie logic + ShareCard scope) |
| **Phase 0.5** | **ADVISABLE** (not mandatory — single-shot dispatch acceptable per spec-panel §3.3, but contract reduces risk) |
| **Dispatch strategy** | Single-shot 5-task TDD (mirror Wave C.2 / Wave D.1, NOT split sub-PR like D.2) |
| **Theme default** | **Light** (sustained Wave D.1 default — D.2 dark is the exception) |
| **Migration pattern** | **Brownfield FORK**: route `/sessions/[id]` currently renders live UI. D.3 must detect `status === 'Completed'` → render summary OR redirect to `/sessions/[id]/live`. Subroute `/sessions/[id]/live` (D.2 SHIPPED) UNTOUCHED. |
| **Estimated effort** | 8-10 days per spec-panel §5 |
| **Bundle target** | ~85 KB (Tier M-L, confetti CSS-only ~3 KB, no PNG export lib) |

**Migration note critical**: Pre-D.3, route `/sessions/[id]` redirected/handled live UI. Post-D.3, route fork:
- `status === 'Completed'` → render `SessionSummaryView` (D.3)
- `status === 'InProgress' | 'Paused'` → `redirect('/sessions/[id]/live')` (server-side, page.tsx)
- `status === 'Setup'` → out-of-scope, fall back to legacy or 404 (decision deferred — see §17 Open questions)

### 1.1 URL state schema (single source of truth, no useState mirror)

```ts
type DiaryFilter = 'all' | 'score' | 'event' | 'chat' | 'photo' | 'tool' | 'agent';
type ShareCardTheme = 'light' | 'dark';
type FixtureKind = 'tied' | 'abandoned' | 'solo' | 'empty-achievements' | 'empty-photos' | 'empty-chat';

interface UrlState {
  diary: DiaryFilter;          // ?diary=all|score|event|chat|photo|tool|agent, default 'all'
  shareTheme: ShareCardTheme;   // ?theme=light|dark (preview-only toggle), default 'light'
  fixture?: FixtureKind;        // ?fixture=... (visual baseline override, gated by STATE_OVERRIDE_ENABLED)
}

// Anti-pattern: NO useState<DiaryFilter> + URL sync hooks.
// Pattern Wave 3 /game-nights drawer (?day=YYYY-MM-DD) sustained.
```

**`?fixture=` URL override** per visual fixture (gated by `STATE_OVERRIDE_ENABLED`):
- `?fixture=tied` → forza podio con 2 giocatori a parità 1° posto
- `?fixture=abandoned` → status=`Abandoned` invece di `Completed` → mostra banner "Sessione abbandonata"
- `?fixture=solo` → 1 partecipante (edge case podio singolo)
- `?fixture=empty-achievements` → achievements array vuoto → render Achievements empty state
- `?fixture=empty-photos` → snapshots array vuoto → render PhotosGallery empty state
- `?fixture=empty-chat` → chat highlights vuoti → render ChatHighlights empty state
- NO override per stato `error` (TanStack `isError` non riproducibile via URL deterministicamente, coperto unit test)

## 2. Route & FSM

### 2.1 6-cell FSM

| # | Cell name | Trigger condition | UI Behavior |
|---|-----------|-------------------|-------------|
| 1 | `loading` | Any of 4 hooks pending (`isLoading=true`) | Skeleton shells per ogni section (Hero, KPI, Diary, Photos, Chat, Share, PlayAgain) |
| 2 | `error` | Any hook errored (`isError=true`, network/5xx) | Shell error card top-of-page + section-level error placeholders sotto (graceful degrade) |
| 3 | `not-found` | `useSessionDetail` returns `null` o 404 | Hero illustrato "Sessione non trovata" + CTA back to /sessions |
| 4 | `not-completed` | `session.status !== 'Completed'` | **Server-side redirect** `/sessions/[id]/live` se `InProgress\|Paused`. Banner inline "Sessione in corso" + link se `Setup` |
| 5 | `default` | All hooks success + `status === 'Completed'` | Full render: Hero podium + ConnectionBar + KPI + Scoring + Achievements + Diary + Photos + Chat + ShareCard + PlayAgain |
| 6 | `partial` | Hook success ma sub-arrays vuoti (no photos OR no achievements OR no chat) | Render con `EmptyXyz` placeholders inline per sections vuote (sustaining other sections) |

### 2.2 Cell × component matrix

| Component | Cell 1 (loading) | Cell 2 (error) | Cell 3 (not-found) | Cell 4 (not-completed) | Cell 5 (default) | Cell 6 (partial) |
|-----------|------------------|----------------|--------------------|-----------------------|-----------------|------------------|
| SummaryHeroPodium | Skeleton podium | Hidden | Hidden | Hidden | Render | Render |
| ConnectionBar | Skeleton | Hidden | Hidden | Hidden | Render | Render |
| SessionKpiGrid | Skeleton 4 KPI | Hidden | Hidden | Hidden | Render | Render |
| ScoringBreakdownTable | Skeleton table | Error placeholder | Hidden | Hidden | Render | Render |
| AchievementsCarousel | Skeleton 3 cards | Error placeholder | Hidden | Hidden | Render | EmptyAchievements if 0 |
| SessionDiaryTimeline | Skeleton turns | Error placeholder | Hidden | Hidden | Render | Empty state if 0 turns |
| PhotosGallery | Skeleton tiles | Error placeholder | Hidden | Hidden | Render | EmptyPhotos if 0 |
| ChatHighlights | Skeleton 2 msg | Error placeholder | Hidden | Hidden | Render | EmptyChat if 0 |
| SessionShareCard | Skeleton preview | Hidden | Hidden | Hidden | Render | Render |
| PlayAgainCta | Skeleton CTA | Hidden | Hidden | Hidden | Render | Render |
| Confetti | Hidden | Hidden | Hidden | Hidden | Render (first-load + WCAG 2.3.3) | Render |

### 2.3 State derivation function

```ts
// apps/web/src/lib/session-summary/session-summary-state.ts
export type SessionSummaryUiState = 'loading' | 'error' | 'not-found' | 'not-completed' | 'default' | 'partial';

export function deriveSessionSummaryUiState(input: {
  sessionId: string | null;
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
  status: string | null;          // session.status
  hasAchievements: boolean;        // achievements.length > 0
  hasPhotos: boolean;              // snapshots.length > 0
  hasChat: boolean;                // chatHighlights.length > 0
}): SessionSummaryUiState {
  if (input.sessionId == null) return 'not-found';   // Cell 3 (invalid id)
  if (input.isLoading) return 'loading';              // Cell 1
  if (input.isError) return 'error';                  // Cell 2
  if (!input.hasData) return 'not-found';             // Cell 3 (404 logical)
  if (input.status !== 'Completed') return 'not-completed'; // Cell 4
  // Cell 5 vs 6: any sub-array empty → partial
  if (!input.hasAchievements || !input.hasPhotos || !input.hasChat) return 'partial';
  return 'default';                                    // Cell 5
}
```

## 3. Hook composition matrix

```
useParams<{id}> ──validate──→ sessionId: string | null
                                  │
                                  │ (null se params?.id è undefined o "")
                                  │
                                  ├─→ useSessionDetail(sessionId, enabled=true) [PARENT]
                                  │       └─→ STATE: idle | loading | error | success(GameSessionDto | null)
                                  │              │
                                  │              ├─ session.status — 'Completed' | 'InProgress' | 'Paused' | 'Abandoned' | 'Setup'
                                  │              ├─ session.participants — ReadonlyArray<ParticipantDto>
                                  │              ├─ session.scores — ReadonlyArray<ScoreEntryDto>
                                  │              ├─ session.finalizedAt — string | null
                                  │              │
                                  │              └──→ gating sub-hooks (only mount if status === 'Completed')
                                  │
                                  ├─→ useSessionDiaryQuery({ sessionId, enabled }) [SUB]
                                  │       └─→ STATE: events grouped by turn ReadonlyArray<DiaryTurn>
                                  │
                                  ├─→ useSessionVisionSnapshots(sessionId, enabled) [SUB]
                                  │       └─→ STATE: ReadonlyArray<SnapshotInfo>
                                  │
                                  └─→ useSessionAchievements({ sessionId, enabled }) [SUB — STUB v1]
                                          └─→ STATE: ReadonlyArray<AchievementDto> via fixture (no backend yet)

                                  ├─→ useCompleteGameNight() mutation [PRE-EXISTING — for PlayAgainCta]
```

### 3.1 Hook gating matrix

| Hook | Enabled when | staleTime | Notes |
|------|--------------|-----------|-------|
| `useSessionDetail` | `!!sessionId` | **1h** (overridden from 15s default — sessions immutable post-endgame, Newman §3.3) | Parent — must succeed before children mount |
| `useSessionDiaryQuery` | `!!sessionId && parent.isSuccess && data?.status === 'Completed'` | 1h | Filterable client-side via `?diary=` |
| `useSessionVisionSnapshots` | `!!sessionId && parent.isSuccess && data?.status === 'Completed'` | 1h | Photos with timestamps |
| `useSessionAchievements` (STUB) | `!!sessionId && parent.isSuccess && data?.status === 'Completed'` | 1h | **Stub via fixture** — no backend endpoint v1 |
| `useCompleteGameNight` | mutation, on demand | — | Wired to `PlayAgainCta` |

### 3.2 SessionId resolution

Identico a Wave C.1 sez. 2.1 e Wave D.2 sez. 2.1:

```ts
const params = useParams<{ id: string }>();
const rawId = params?.id;
const sessionId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

// Anti-pattern (Wave C.1 PR #697 retry):
// const id = params?.id ?? '';   // ❌ creates "undefined" string
```

### 3.3 Cache invalidation

Per Newman §3.3 recommendation: sessions immutable post-endgame, cache 1h.

```ts
// apps/web/src/hooks/queries/useSessionDetail.ts
// Override staleTime when called from D.3 summary context
export function useSessionDetail(sessionId: string, options?: { summaryMode?: boolean }) {
  return useQuery({
    queryKey: sessionDetailKeys.detail(sessionId),
    queryFn: () => api.sessions.getById(sessionId),
    enabled: !!sessionId,
    staleTime: options?.summaryMode ? 60 * 60 * 1000 : 15 * 1000,
    refetchOnWindowFocus: !options?.summaryMode,  // disable for completed sessions
  });
}
```

**Out-of-scope**: chat highlights edit (admin moderator action) → would invalidate cache, defer to future.

## 4. Schema contract

### 4.1 Existing schemas (verified, schema reality v1 audit Gate B)

```ts
// apps/web/src/lib/api/schemas/session-tracking.schemas.ts (verified in repo)
export const ParticipantDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  displayName: z.string(),
  isOwner: z.boolean(),
  joinOrder: z.number().int(),
  finalRank: z.number().int().nullable(),    // ✅ AVAILABLE
  totalScore: z.number(),                     // ✅ AVAILABLE
});
// ❌ MISSING from backend (frontend MUST handle):
// - tiedWith: ReadonlyArray<string> (computed in §12)
// - color: string (fallback to deterministic hash on userId/displayName)
// - avatarUrl: string | null (fallback to default avatar)
```

### 4.2 Computed types (frontend-only)

```ts
// apps/web/src/lib/session-summary/types.ts

/** Decorated participant after tie-group computation. */
export interface RankedParticipant extends ParticipantDto {
  readonly rank: number;             // computed (1, 2, 2, 4 for tied 2nd)
  readonly isTied: boolean;          // true if rank shared with ≥1 other
  readonly tiedPlayerIds: ReadonlyArray<string>;  // ids sharing same rank
  readonly color: string;            // HSL string from hash(userId/displayName)
  readonly avatarSeed: string;       // for fallback avatar generator
  readonly isWinner: boolean;        // rank === 1
}

/** Stub achievement schema (no backend v1). */
export interface AchievementDto {
  readonly id: string;
  readonly emoji: string;
  readonly name: string;
  readonly description: string;
  readonly unlocked: boolean;
  readonly lockHint?: string;        // e.g., "Servono 25 uova"
  readonly unlockedAt?: string;
}

/** Diary entry shape (verified — backend exists, payload matches). */
export interface DiaryEvent {
  readonly id: string;
  readonly kind: 'score' | 'tool' | 'agent' | 'chat' | 'photo' | 'event';
  readonly timestamp: string;        // ISO-8601
  readonly emoji: string;
  readonly text: string;
}

export interface DiaryTurn {
  readonly turn: number;
  readonly events: ReadonlyArray<DiaryEvent>;
}

/** Composed summary DTO assembled by orchestrator. */
export interface SessionSummaryComposed {
  readonly sessionId: string;
  readonly gameName: string | null;
  readonly status: string;
  readonly finalizedAt: string | null;
  readonly durationMinutes: number | null;
  readonly participants: ReadonlyArray<RankedParticipant>;
  readonly winner: RankedParticipant | null;     // null if abandoned/all-tied
  readonly diaryTurns: ReadonlyArray<DiaryTurn>;
  readonly snapshots: ReadonlyArray<SnapshotInfo>;
  readonly achievements: ReadonlyArray<AchievementDto>;
  readonly chatHighlights: ReadonlyArray<ChatHighlight>;  // stub — see §4.3
}
```

### 4.3 Stub schemas pending backend (Gate B documentation)

```ts
/** Chat highlights — STUB. No backend v1, fixture only. */
export interface ChatHighlight {
  readonly id: string;
  readonly senderName: string;
  readonly content: string;
  readonly timestamp: string;
  readonly highlightedBy: 'user' | 'agent';
}
```

**Schema reality v1 carryover** (Gate B): ChatHighlight + AchievementDto are **frontend-only stubs**. Backend impl deferred.

**Pre-implementation grep audit** (mandatory):
```bash
# Verify no chat highlights endpoint
grep -rn "chat-highlights\|ChatHighlight" apps/api/src/Api/BoundedContexts/SessionTracking/ || echo "CONFIRMED stub-only"

# Verify no achievements endpoint
grep -rn "/achievements\|AchievementDto" apps/api/src/Api/BoundedContexts/SessionTracking/ || echo "CONFIRMED stub-only"

# Verify ParticipantDto fields used in mockup (color, avatar) NOT in backend
grep -n "Color\|AvatarUrl\|TiedWith" apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/ParticipantDto.cs || echo "CONFIRMED missing — frontend computes"
```

## 5. Component specs

> **Audit Gate C applied** (MeepleCard API-fit): D.3 = post-game summary cards. Most sections DIVERGE from MeepleCard:
> - **SummaryHeroPodium**: bespoke 3-place vertical layout with confetti + tied indicator. MeepleCard cannot accommodate (RICHER). DIVERGE.
> - **AchievementsCarousel**: card grid with locked/unlocked variant. Could fit MeepleCard, but emoji-first design + lock state inversion → DIVERGE for clarity.
> - **PhotosGallery**: image tile grid. DIVERGE (MeepleCard not designed for 1:1 image tiles).
> - **Other components**: bespoke detail-page sections (table, timeline, banner, dialog). All DIVERGE.

### 5.1 SummaryHeroPodium

```ts
interface SummaryHeroPodiumProps {
  readonly winner: RankedParticipant | null;        // null if all-abandoned
  readonly podium: ReadonlyArray<RankedParticipant>; // top 3 (or fewer if N<3)
  readonly gameName: string | null;
  readonly durationMinutes: number | null;
  readonly playerCount: number;
  readonly status: 'Completed' | 'Abandoned';
  readonly variant: 'default' | 'tied' | 'abandoned' | 'solo';
  readonly compact?: boolean;
  readonly labels: SummaryHeroPodiumLabels;
}

interface SummaryHeroPodiumLabels {
  readonly winnerHeadline: string;          // "{name} vince {game}" (resolved by orchestrator)
  readonly tiedHeadline: string;            // "{names} pareggiano {game}"
  readonly abandonedHeadline: string;       // "Sessione abbandonata"
  readonly soloHeadline: string;            // "Sessione solo — {name}"
  readonly subtitleTemplate: string;        // "{date} · {duration} · {playerCount} giocatori" (ICU plural)
  readonly podiumPlaceAriaLabel: string;    // "Posizione {place}: {name} con {score} punti"
  readonly tiedAriaLabel: string;           // "Parità con {names}"
}
```

**Render rules** (Gate C divergence justified):
- 3 layered concentric circles → larger center for 1° (winner), smaller flanks for 2°/3°
- Winner ring with `eHs('toolkit')` border (gold accent)
- Tied: position offset by `=` indicator (visual)
- Abandoned variant: gray-tone palette, no confetti
- Solo: only 1 circle centered

**A11y**:
- `role="region" aria-labelledby={headlineId}`
- Winner ring `<div aria-label={t('podiumPlaceAriaLabel', { place: 1, name, score })}>`
- Tied state announced via `aria-describedby` linking to tied list

### 5.2 Confetti

```ts
interface ConfettiProps {
  readonly enabled: boolean;            // false if status !== 'Completed' OR sessionStorage flag set
  readonly sessionId: string;            // for sessionStorage key
  readonly labels: { readonly reducedMotionFallbackAlt: string }; // alt text for static medal
}
```

**WCAG 2.3.3 contract** (Wiegers §3.3):
- CSS-only `@keyframes mai-confetti-fall` → 12 confetti pieces falling
- Element wrapper `<div data-slot="confetti" aria-hidden="true">` (decorative)
- `@media (prefers-reduced-motion: reduce)` → animation disabled, render static medal icon `<span role="img" aria-label={t('reducedMotionFallbackAlt')}>🏅</span>`
- First-load only: `sessionStorage.getItem('meeplai-d3-confetti-${sessionId}')` → if set, skip animation (prevent re-trigger on tab switch)
- After mount, set `sessionStorage.setItem('meeplai-d3-confetti-${sessionId}', '1')`
- **Visual baselines MASK confetti element** (Crispin §3.3): `await page.locator('[data-slot="confetti"]').evaluate(el => el.style.visibility = 'hidden')` before screenshot

**Pre-implementation rule**: NO `canvas-confetti` or animation lib. Pure CSS only.

### 5.3 ConnectionBar

```ts
interface ConnectionBarProps {
  readonly status: 'Completed' | 'Abandoned';
  readonly finalizedAt: string;          // ISO-8601
  readonly durationMinutes: number | null;
  readonly playerCount: number;
  readonly compact?: boolean;
  readonly labels: ConnectionBarLabels;
}

interface ConnectionBarLabels {
  readonly statusLabel: string;          // "Sessione completata" or "Sessione abbandonata"
  readonly finalizedAtTemplate: string;  // ICU date format
  readonly durationTemplate: string;     // "{duration} di gioco" (ICU plural minutes/hours)
  readonly playerCountTemplate: string;  // "{count, plural, one {# giocatore} other {# giocatori}}"
}
```

**A11y**: `role="status"` (sealed completion info, no live updates).

### 5.4 SessionKpiGrid

```ts
interface SessionKpiGridProps {
  readonly kpis: ReadonlyArray<KpiEntry>;
  readonly compact?: boolean;
  readonly labels: { readonly gridAriaLabel: string };
}

interface KpiEntry {
  readonly key: 'total-points' | 'rounds' | 'avg-turn-time' | 'mvp-action';
  readonly label: string;
  readonly value: string;                // already formatted (e.g., "143" or "2m 45s")
  readonly emoji: string;
  readonly trend?: 'up' | 'down' | 'neutral';
}
```

**A11y**: `<dl>` semantic markup with `<dt>` label + `<dd>` value pairs.

### 5.5 ScoringBreakdownTable

```ts
interface ScoringBreakdownTableProps {
  readonly participants: ReadonlyArray<RankedParticipant>;
  readonly scoreCategories: ReadonlyArray<string>; // e.g., ['Birds', 'Eggs', 'Bonus']
  readonly scoreMatrix: ReadonlyMap<string, ReadonlyMap<string, number>>; // playerId → category → score
  readonly compact?: boolean;
  readonly labels: ScoringBreakdownTableLabels;
}
```

**Render**: standard `<table>` with `<thead>` (categories) + `<tbody>` (participants). Sticky first column on mobile. Tied players adjacent rows, marked with `=` icon.

**A11y**: `<th scope="col">` + `<th scope="row">` for screen readers.

### 5.6 AchievementsCarousel

```ts
interface AchievementsCarouselProps {
  readonly achievements: ReadonlyArray<AchievementDto>;
  readonly compact?: boolean;
  readonly labels: AchievementsCarouselLabels;
}

interface AchievementsCarouselLabels {
  readonly sectionTitle: string;
  readonly unlockedCountTemplate: string;        // "{unlocked} / {total} sbloccati" (ICU plural)
  readonly lockedAriaPrefix: string;             // "Bloccato: "
  readonly emptyTitle: string;
  readonly emptyDescription: string;
}
```

**Empty state** (Cell 6 partial): if `achievements.length === 0`, render `EmptyAchievements` placeholder per mockup (dashed border + 🏅 icon + "Nessun achievement").

**A11y**: locked items `aria-label="Bloccato: {name} — {lockHint}"`.

### 5.7 SessionDiaryTimeline

```ts
interface SessionDiaryTimelineProps {
  readonly turns: ReadonlyArray<DiaryTurn>;
  readonly activeFilter: DiaryFilter;
  readonly onFilterChange: (next: DiaryFilter) => void;
  readonly expandedTurns: ReadonlySet<number>;
  readonly onToggleTurn: (turn: number) => void;
  readonly compact?: boolean;
  readonly labels: SessionDiaryTimelineLabels;
}
```

**Filter pills** roving tabindex via `useTablistKeyboardNav` (PR #623 reuse). Filter changes update URL `?diary=` (orchestrator).

**Empty state**: if filter applied + no events match → "Nessun evento per questo filtro".

**A11y**: each turn collapsible button has `aria-expanded` + `aria-controls`.

### 5.8 PhotosGallery

```ts
interface PhotosGalleryProps {
  readonly snapshots: ReadonlyArray<SnapshotInfo>;
  readonly compact?: boolean;
  readonly onPhotoClick?: (snapshot: SnapshotInfo) => void;  // out-of-scope D.3 (lightbox future)
  readonly labels: PhotosGalleryLabels;
}
```

**Empty state** (Cell 6 partial): if `snapshots.length === 0`, render `EmptyPhotos` placeholder.

**Layout**: 3-col grid desktop, 2-col mobile.

**A11y**: each `<img alt={snapshot.description ?? 'Foto sessione'}>`.

### 5.9 ChatHighlights

```ts
interface ChatHighlightsProps {
  readonly highlights: ReadonlyArray<ChatHighlight>;
  readonly expanded: boolean;
  readonly onToggleExpand: () => void;
  readonly compact?: boolean;
  readonly labels: ChatHighlightsLabels;
}
```

**Empty state** (Cell 6 partial): if `highlights.length === 0`, render `EmptyChat` placeholder.

**Collapse**: collapsible section with `aria-expanded` button.

### 5.10 SessionShareCard

```ts
interface SessionShareCardProps {
  readonly summary: Pick<SessionSummaryComposed, 'gameName' | 'finalizedAt' | 'durationMinutes' | 'participants' | 'winner'>;
  readonly previewTheme: ShareCardTheme;
  readonly onPreviewThemeChange: (next: ShareCardTheme) => void;
  readonly onShare: (channel: 'twitter' | 'instagram' | 'whatsapp' | 'copy') => void;
  readonly compact?: boolean;
  readonly labels: SessionShareCardLabels;
}
```

**Scope** (Cockburn §3.3 out-of-scope clarification):
- ✅ Preview-only (light/dark toggle works in DOM, theme prop drives render)
- ✅ Share buttons:
  - `twitter` → Web Share API if available, else `window.open(twitterIntentUrl)`
  - `instagram` → `navigator.clipboard.writeText(shareText)` + toast "Testo copiato — incolla su Instagram"
  - `whatsapp` → `window.open('https://wa.me/?text=' + encodeURIComponent(shareText))`
  - `copy` → `navigator.clipboard.writeText(shareLink)` + toast "Link copiato"
- ❌ "Download PNG" button → **DISABLED** + tooltip "Coming soon" (out-of-scope, no html2canvas)

**A11y**: `role="radiogroup"` on theme toggle + `aria-checked` on each option.

### 5.11 PlayAgainCta

```ts
interface PlayAgainCtaProps {
  readonly sessionId: string;
  readonly gameName: string | null;
  readonly playerNames: ReadonlyArray<string>;
  readonly onSamePlayers: () => void;        // calls useCompleteGameNight().mutate({ ... })
  readonly onDifferentPlayers: () => void;   // navigates to /sessions/new?fromId={id}
  readonly isPending: boolean;
  readonly labels: PlayAgainCtaLabels;
}
```

**A11y**: primary CTA `<button>` with `aria-busy={isPending}` during mutation.

## 6. URL state SSOT

Per Wave C.1/D.2 pattern, URL is single source of truth — no `useState` mirror.

```ts
// apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx

const searchParams = useSearchParams();
const router = useRouter();
const pathname = usePathname();

// Derive state from URL
const diaryFilter = (searchParams.get('diary') as DiaryFilter | null) ?? 'all';
const shareTheme = (searchParams.get('theme') as ShareCardTheme | null) ?? 'light';
const fixtureOverride = STATE_OVERRIDE_ENABLED ? (searchParams.get('fixture') as FixtureKind | null) : null;

// Update URL helper
const setUrlParam = useCallback((key: string, value: string | null) => {
  const next = new URLSearchParams(searchParams);
  if (value === null) next.delete(key);
  else next.set(key, value);
  router.replace(`${pathname}?${next.toString()}`, { scroll: false });
}, [searchParams, router, pathname]);
```

**Defensive**: `STATE_OVERRIDE_ENABLED` gate ensures `?fixture=` only works in dev/visual-test builds:

```ts
// apps/web/src/lib/session-summary/state-override.ts
export const STATE_OVERRIDE_ENABLED =
  process.env.NODE_ENV !== 'production' ||
  process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';
```

## 7. Bundle budget

Tier M-L per spec sez. 5: ~85 KB target.

| Component / Module | Estimated KB |
|--------------------|--------------|
| 11 v2 components | ~45 KB |
| Orchestrator (SessionSummaryView) | ~14 KB |
| Foundation lib (state derivation, tie-group, fixture, sse-events not needed) | ~9 KB |
| Confetti CSS-only | ~3 KB |
| i18n keys (it+en, ICU plural) | ~10 KB |
| useSessionAchievements stub fixture | ~4 KB |

**Subtotal**: ~85 KB.

**Current baseline post-D.2**: 13,718,922 bytes.
**Target post-D.3**: ~13,805,000 bytes (delta +~85 KB, well within wave D 250 KB DoD).

**Wave D total budget tracking**:
- D.1 actual: ~80 KB
- D.2 actual: ~115 KB (incl. lazy dialogs)
- D.3 target: ~85 KB
- **Total wave D**: ~280 KB (5 KB over 250 KB DoD by current estimate — see §17 Open questions for mitigation)

## 8. i18n keys (`pages.sessionSummary.*` namespace)

Both `apps/web/src/locales/it.json` and `apps/web/src/locales/en.json` MUST include ~40 keys minimum. ICU plural patterns mandatory for count templates (Gate A).

```json
{
  "pages": {
    "sessionSummary": {
      "hero": {
        "winnerHeadline": "{name} vince {game}",
        "tiedHeadline": "Pareggio: {names} con {score} punti",
        "abandonedHeadline": "Sessione abbandonata",
        "soloHeadline": "Sessione solo — {name}",
        "subtitleTemplate": "{date} · {duration} · {count, plural, one {# giocatore} other {# giocatori}}",
        "podiumPlaceAriaLabel": "Posizione {place}: {name} con {score} punti",
        "tiedAriaLabel": "Parità con {names}"
      },
      "confetti": {
        "reducedMotionFallbackAlt": "Medaglia vittoria"
      },
      "connectionBar": {
        "completedLabel": "Sessione completata",
        "abandonedLabel": "Sessione abbandonata",
        "finalizedAtTemplate": "Terminata il {date}",
        "durationTemplate": "{minutes, plural, one {# minuto} other {# minuti}} di gioco",
        "playerCountTemplate": "{count, plural, one {# giocatore} other {# giocatori}}"
      },
      "kpi": {
        "gridAriaLabel": "Statistiche partita",
        "totalPoints": "Punti totali",
        "rounds": "Turni",
        "avgTurnTime": "Tempo medio turno",
        "mvpAction": "Azione MVP"
      },
      "scoring": {
        "sectionTitle": "Punteggio dettagliato",
        "tableAriaLabel": "Punteggi per categoria",
        "tiedIndicator": "= "
      },
      "achievements": {
        "sectionTitle": "Achievements",
        "unlockedCountTemplate": "{unlocked} / {total} sbloccati",
        "lockedAriaPrefix": "Bloccato: ",
        "emptyTitle": "Nessun achievement questa partita",
        "emptyDescription": "Riprova per sbloccare nuovi badge!"
      },
      "diary": {
        "sectionTitle": "Diario partita",
        "filterAll": "Tutti",
        "filterScore": "Score",
        "filterEvent": "Eventi",
        "filterChat": "Chat",
        "filterPhoto": "Foto",
        "filterTool": "Strumenti",
        "filterAgent": "Agente",
        "emptyForFilter": "Nessun evento per questo filtro",
        "turnHeadingTemplate": "Turno {turn}",
        "expandTurnAriaLabel": "Espandi turno {turn}",
        "collapseTurnAriaLabel": "Comprimi turno {turn}"
      },
      "photos": {
        "sectionTitle": "Foto sessione",
        "photoCountTemplate": "{count, plural, one {# foto} other {# foto}}",
        "emptyTitle": "Nessuna foto",
        "emptyDescription": "Le foto scattate durante la sessione appariranno qui",
        "photoAltFallback": "Foto sessione"
      },
      "chatHighlights": {
        "sectionTitle": "Chat highlights",
        "expandAriaLabel": "Espandi chat",
        "collapseAriaLabel": "Comprimi chat",
        "emptyTitle": "Nessun highlight",
        "emptyDescription": "I momenti salienti della chat appariranno qui"
      },
      "shareCard": {
        "sectionTitle": "Share card preview",
        "themeToggleAriaLabel": "Tema preview",
        "themeLight": "Light",
        "themeDark": "Dark",
        "shareTwitter": "Twitter",
        "shareInstagram": "Instagram",
        "shareWhatsapp": "WhatsApp",
        "shareCopy": "Copia link",
        "shareDownloadPng": "Download PNG",
        "shareDownloadPngTooltip": "Coming soon",
        "shareSuccessLink": "Link copiato",
        "shareSuccessText": "Testo copiato — incolla su Instagram"
      },
      "playAgain": {
        "sectionTitle": "Pronti per la rivincita?",
        "description": "Stessi giocatori e gioco — agente già caricato.",
        "samePlayersCta": "Riavvia con stessi player",
        "differentPlayersCta": "Cambia giocatori"
      },
      "states": {
        "loadingTitle": "Caricamento...",
        "errorTitle": "Errore",
        "errorRetryCta": "Riprova",
        "notFoundTitle": "Sessione non trovata",
        "notFoundDescription": "La sessione richiesta non esiste o è stata eliminata",
        "notFoundBackCta": "Torna a Sessioni",
        "notCompletedTitle": "Sessione in corso",
        "notCompletedLiveCta": "Vai alla sessione live"
      }
    }
  }
}
```

**Total**: ~40 keys per locale. ICU plural on `count` placeholders (Gate A).

**English mirror**: same structure, translated values. Pre-impl audit:
```bash
grep -c "\"sessionSummary\"" apps/web/src/locales/it.json apps/web/src/locales/en.json
# Expect both = 1 after impl
```

## 9. Confetti contract

**Wiegers §3.3 + WCAG 2.3.3 (Pause/Stop/Hide) compliance**:

```css
/* apps/web/src/components/v2/session-summary/confetti.module.css */
@keyframes mai-confetti-fall {
  0% { transform: translateY(-20px) rotate(0deg); opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { transform: translateY(100vh) rotate(720deg); opacity: 0; }
}

.confettiPiece {
  position: absolute;
  width: 8px;
  height: 8px;
  animation: mai-confetti-fall 3s linear forwards;
}

@media (prefers-reduced-motion: reduce) {
  .confettiPiece { animation: none; display: none; }
  .reducedMotionFallback { display: inline-block; font-size: 48px; }
}
```

**Component**:
```tsx
export function Confetti({ enabled, sessionId, labels }: ConfettiProps) {
  const [showAnim, setShowAnim] = useState(false);

  useEffect(() => {
    if (!enabled) return;
    const key = `meeplai-d3-confetti-${sessionId}`;
    if (typeof window === 'undefined' || sessionStorage.getItem(key)) return;
    setShowAnim(true);
    sessionStorage.setItem(key, '1');
  }, [enabled, sessionId]);

  if (!enabled) return null;

  return (
    <div data-slot="confetti" aria-hidden="true">
      {showAnim ? (
        Array.from({ length: 12 }).map((_, i) => (
          <span key={i} className={styles.confettiPiece} style={{ left: `${(i * 8) % 100}%`, background: confettiColors[i % 4] }} />
        ))
      ) : null}
      <span className={styles.reducedMotionFallback} role="img" aria-label={labels.reducedMotionFallbackAlt}>🏅</span>
    </div>
  );
}
```

**Visual baselines MASK** (Gate D + Crispin §3.3):
```ts
// e2e/visual-migrated/sp4-session-summary.spec.ts
await page.goto('/sessions/<fixture-id>');
await page.waitForSelector('[data-slot="session-summary-view"]');
// Mask non-deterministic confetti before screenshot
await page.locator('[data-slot="confetti"]').evaluate(el => { el.style.visibility = 'hidden'; });
await expect(page).toHaveScreenshot('sp4-session-summary-default-desktop.png');
```

## 10. ShareCard scope

**Cockburn §3.3 out-of-scope clarification**:

| Feature | Status | Implementation |
|---------|--------|----------------|
| Light/dark theme preview | ✅ IN SCOPE | URL state `?theme=` + theme prop drives DOM |
| Twitter share | ✅ IN SCOPE | Web Share API + fallback `window.open()` |
| Instagram share | ✅ IN SCOPE | Clipboard write + toast |
| WhatsApp share | ✅ IN SCOPE | `wa.me/?text=` URL scheme |
| Copy link | ✅ IN SCOPE | `navigator.clipboard.writeText()` + toast |
| Download PNG | ❌ **OUT OF SCOPE** | Button rendered DISABLED + tooltip "Coming soon" |
| Server-side image generation | ❌ OUT OF SCOPE | Backend impl deferred (per #582) |
| html2canvas / dom-to-image lib | ❌ FORBIDDEN | Bundle bloat ~20 KB unjustified |

**Rationale**: ShareCard is preview-only this PR. Actual image rendering = backend service (future epic).

## 11. Theme support

**Default**: light theme (sustained Wave D.1, D.2 dark default is the exception for live UI).

| Theme aspect | Value |
|--------------|-------|
| Page-level theme | `data-theme="light"` (default — orchestrator does NOT override) |
| ShareCard preview toggle | Independent `?theme=` URL param drives ShareCard component preview only |
| ShareCard theme prop | Component-internal — does NOT change page theme |
| Visual baselines | 2 PNG (desktop+mobile) per fixture variant — no dark theme baselines for whole page |

**ShareCard preview toggle isolation**: changing `?theme=dark` MUST NOT affect rest of page. Apply theme via inline styles on `SessionShareCard` root only:

```tsx
<section data-slot="session-share-card" data-preview-theme={previewTheme} style={{ background: previewTheme === 'dark' ? '#0f0c1e' : '#fff' }}>
```

## 12. Tie-group computation

**Wiegers §3.3 + spec-panel recommendation**: tie = same `totalScore` → both displayed at same place height + visual `=` indicator.

**Tie-breaker order**: alphabetical first name (deterministic — locale-aware `localeCompare`).

```ts
// apps/web/src/lib/session-summary/tie-groups.ts
export function computeTieGroups(participants: ReadonlyArray<ParticipantDto>): ReadonlyArray<RankedParticipant> {
  // Sort by score desc, then by displayName asc (tie-breaker)
  const sorted = [...participants].sort((a, b) => {
    if (b.totalScore !== a.totalScore) return b.totalScore - a.totalScore;
    return a.displayName.localeCompare(b.displayName, undefined, { sensitivity: 'base' });
  });

  // Compute rank with ties
  let currentRank = 1;
  const result: RankedParticipant[] = [];
  for (let i = 0; i < sorted.length; i++) {
    const p = sorted[i];
    if (i > 0 && sorted[i - 1].totalScore !== p.totalScore) {
      currentRank = i + 1;  // skip tied positions (1, 2, 2, 4 not 1, 2, 2, 3)
    }
    const tiedPlayerIds = sorted
      .filter(q => q.totalScore === p.totalScore)
      .map(q => q.id);
    result.push({
      ...p,
      rank: currentRank,
      isTied: tiedPlayerIds.length > 1,
      tiedPlayerIds,
      color: hashToHsl(p.userId ?? p.displayName),
      avatarSeed: p.userId ?? p.id,
      isWinner: currentRank === 1,
    });
  }
  return result;
}

function hashToHsl(seed: string): string {
  // Deterministic hash → HSL string fallback (frontend-only color)
  let hash = 0;
  for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) | 0;
  const hue = Math.abs(hash) % 360;
  return `${hue}, 65%, 55%`;
}
```

### 12.1 Test cases (Gherkin scenarios)

```gherkin
Feature: Tie-group computation

  Scenario: All distinct scores
    Given participants [Alice 100, Bob 90, Carol 80]
    When computeTieGroups is called
    Then result has ranks [1, 2, 3]
    And no participant is tied

  Scenario: 2-tied at 1st place
    Given participants [Alice 100, Bob 100, Carol 90]
    When computeTieGroups is called
    Then ranks are [1, 1, 3] (skip 2nd position)
    And Alice and Bob both have isTied=true
    And tiedPlayerIds for Alice = [Alice.id, Bob.id]
    And tie-breaker: Alice listed before Bob (alphabetical)

  Scenario: 3-tied at 2nd place
    Given participants [Alice 100, Bob 80, Carol 80, Dan 80]
    When computeTieGroups is called
    Then ranks are [1, 2, 2, 2]
    And winner is Alice (rank 1, isTied=false)
    And Bob/Carol/Dan all isTied=true

  Scenario: All-tied (edge)
    Given participants [Alice 50, Bob 50, Carol 50]
    When computeTieGroups is called
    Then all have rank 1
    And all have isTied=true
    And winner is Alice (alphabetical first, isWinner=true)
    And SummaryHeroPodium variant='tied' with all 3 in podium

  Scenario: Solo participant
    Given participants [Alice 100]
    When computeTieGroups is called
    Then rank is [1]
    And Alice.isTied=false
    And SummaryHeroPodium variant='solo'

  Scenario: Tie-breaker accent-insensitive
    Given participants [Étienne 100, Bob 100]
    When computeTieGroups is called
    Then tie-breaker locale-aware: Bob listed before Étienne (B < É)
```

## 13. Four audit gates (Gate A-D applied)

⚠️ **Verificare PRIMA del dispatch implementation subagent**:

### Gate A — ICU plural defensive pattern (#741 §10.2 Gate A)

- [ ] Audit i18n keys con `{count, plural, ...}` ICU formatter:
  - `pages.sessionSummary.hero.subtitleTemplate` (player count)
  - `pages.sessionSummary.connectionBar.durationTemplate` (minutes)
  - `pages.sessionSummary.connectionBar.playerCountTemplate`
  - `pages.sessionSummary.photos.photoCountTemplate`
  - `pages.sessionSummary.diary.expandTurnAriaLabel` (turn number)
- [ ] Orchestrator usa `t(key, { count })` — MAI `intl.messages[key].replace(...)`
- [ ] Component riceve `string` resolto (NOT template + count)
- [ ] Pre-impl grep audit:
  ```bash
  grep "{count, plural" apps/web/src/locales/*.json | grep sessionSummary
  ```

### Gate B — Schema reality v1 carryover (#741 §10.2 Gate B)

- [ ] **ParticipantDto field audit**: `grep "Color\|AvatarUrl\|TiedWith" apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/ParticipantDto.cs`
  - **Expected**: confirmed missing → frontend computes via `hashToHsl()` + tie-groups
- [ ] **Achievements endpoint audit**: confirm no `/achievements` endpoint exists
  - **Expected**: STUB v1 via `useSessionAchievements` fixture
- [ ] **Chat highlights endpoint audit**: confirm no `/chat-highlights` endpoint exists
  - **Expected**: STUB v1 via fixture
- [ ] **Diary turn shape audit**: `grep "DiaryEntry\|GetDiaryAsync" apps/api/src/Api/BoundedContexts/SessionTracking/`
  - Map mockup `DIARY` shape to actual backend `useSessionDiaryQuery` payload — adapter if needed
- [ ] Document v1 carryover gaps in `lib/session-summary/types.ts` JSDoc per stub schema

### Gate C — MeepleCard API-fit audit (#741 §10.2 Gate C)

- [x] **Decision**: ALL 11 D.3 components DIVERGE from MeepleCard
  - SummaryHeroPodium: 3-place vertical layout with confetti, RICHER than MeepleCard
  - AchievementsCarousel: emoji-first with locked/unlocked variant, MeepleCard not designed for this
  - PhotosGallery: image tile grid (1:1 thumbs), MeepleCard not designed for image-first
  - Other 8 components: detail-page sections (table, timeline, banner, dialog), bespoke
- [x] Justification documented per component (§5)
- [x] No MeepleCard wrap attempts during impl

### Gate D — Bootstrap-then-merge discipline (#741 §10.2 Gate D)

- [ ] PR opens → trigger `gh workflow run visual-regression-migrated.yml -f mode=bootstrap`
- [ ] Wait for PNG baselines committed to branch (6 PNG total — see §14)
- [ ] Verify `[data-slot="confetti"]` masked correctly (deterministic screenshot)
- [ ] Verify visual regression passes against baselines (2nd run mode=verify)
- [ ] THEN consider merge (--admin only if E2E DB flake confirmed pre-existing)

## 14. Visual baselines matrix

**Crispin §3.3 + Gate D**: 6 baselines total. Confetti masked in all.

| Baseline name | Fixture override | Viewport | Theme | Description |
|---------------|------------------|----------|-------|-------------|
| `sp4-session-summary-default-desktop.png` | none (default) | 1280×720 | light | Happy path — Cell 5 default with all sections |
| `sp4-session-summary-default-mobile.png` | none (default) | 375×812 | light | Mobile layout — Cell 5 default |
| `sp4-session-summary-tied-desktop.png` | `?fixture=tied` | 1280×720 | light | 2 players tied 1st place — variant='tied' |
| `sp4-session-summary-abandoned-desktop.png` | `?fixture=abandoned` | 1280×720 | light | Abandoned status — variant='abandoned' |
| `sp4-session-summary-empty-photos-desktop.png` | `?fixture=empty-photos` | 1280×720 | light | Cell 6 partial — PhotosGallery empty state |
| `sp4-session-summary-share-card-dark-desktop.png` | `?theme=dark` | 1280×720 | (light page, dark ShareCard) | ShareCard preview toggle dark — independent of page theme |

**NOT covered visually** (covered by unit tests instead):
- Cell 1 loading (skeleton non-deterministic)
- Cell 2 error (TanStack `isError` non-reproducible deterministically via URL)
- Cell 3 not-found (covered by route handling test)

## 15. Test matrix

Tier M-L per spec sez. 4.1 ratio: **60% unit / 30% integration / 10% e2e**.

### 15.1 Unit tests (~60% — ~95 tests)

| Target | Tests |
|--------|-------|
| `deriveSessionSummaryUiState` | Cells 1-6 + edge cases — 10 tests |
| `computeTieGroups` | All 6 Gherkin scenarios + edge cases — 12 tests |
| `hashToHsl` | Deterministic for same seed, different for different seeds — 4 tests |
| `parseFixtureOverride` | Valid/invalid/disabled gate — 6 tests |
| 11 v2 components | Render shape per state variant + props — ~50 tests |
| `useSessionAchievements` (stub) | Returns deterministic fixture, gating works — 4 tests |
| Confetti behavior | sessionStorage flag, reduced-motion fallback, enabled prop — 6 tests |
| ShareCard handlers | Each share channel calls correct API — 5 tests |

### 15.2 Integration tests (~30% — ~28 tests)

Orchestrator-level via `renderHook` + MSW mocks:

| Scenario | Cell |
|----------|------|
| `sessionId === null` | Cell 3 |
| `useSessionDetail.loading` | Cell 1 |
| `useSessionDetail.error` | Cell 2 |
| `useSessionDetail.success(null)` | Cell 3 |
| `status === 'InProgress'` → server-side redirect to `/live` | Cell 4 (server-side test) |
| `status === 'Completed'` + all sub-hooks success | Cell 5 |
| `status === 'Completed'` + achievements empty | Cell 6 partial |
| `status === 'Completed'` + photos empty | Cell 6 partial |
| `status === 'Completed'` + chat empty | Cell 6 partial |
| `?diary=score` URL → SessionDiaryTimeline filters | (filter logic) |
| `?theme=dark` URL → ShareCard preview only changes | (theme isolation) |
| Tied podium `?fixture=tied` → variant='tied' rendered | (tie display) |
| `useSessionAchievements` stub gating | (stub fixture) |
| ShareCard "Copy link" → clipboard.writeText called | (share handler) |
| ShareCard "Download PNG" → button DISABLED | (out-of-scope guard) |
| PlayAgainCta same players → `useCompleteGameNight.mutate` called | (mutation wiring) |
| Reduced-motion media query → confetti shows static medal | (a11y) |

**Critical assertions**:
```ts
const detailHandler = (req) => {
  const sessionId = req.params.id;
  expect(sessionId).not.toBe('undefined');
  expect(sessionId).not.toBe('null');
  expect(sessionId).toMatch(/^[a-f0-9-]{36}$/);
  return res(ctx.json(mockSession));
};
```

### 15.3 E2E tests (~10% — ~8 specs)

- `e2e/visual-migrated/sp4-session-summary.spec.ts` — 6 baselines per §14, confetti masked
- `e2e/v2-states/session-summary.spec.ts` — Cells 1, 3, 4, 6 partial states deterministic
- `e2e/a11y/session-summary.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion + tied podium aria + diary filter keyboard nav
- `e2e/smoke-real-backend/session-summary.smoke.spec.ts` — deterministic seeded completed session against staging (optional, defer post-merge)

**A11y test critical** (reduced-motion verification):
```ts
test('Confetti respects prefers-reduced-motion', async ({ page, context }) => {
  await context.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/sessions/<fixture-id>');
  await page.waitForSelector('[data-slot="session-summary-view"]');
  // Confetti pieces should NOT animate
  const confettiPieces = await page.locator('[data-slot="confetti"] [class*="confettiPiece"]').count();
  expect(confettiPieces).toBe(0);
  // Fallback medal should be visible
  const fallback = await page.locator('[data-slot="confetti"] [role="img"]').isVisible();
  expect(fallback).toBe(true);
});
```

**Total expected counts**: ~95 unit + ~28 integration + ~8 e2e = **~131 tests** (Wave C.1 PR #702 reference: ~118 tests; Wave D.1 PR #736: ~173 tests).

## 16. 5-task TDD breakdown

Mirror Wave C.2 / Wave D.1 single-shot 5-task pattern (NOT split sub-PR like D.2).

### Task 1: Foundation (Day 1-2)

**Scope**: lib helpers + i18n + visual fixture + stub achievements hook.

**Deliverables**:
- `apps/web/src/lib/session-summary/session-summary-state.ts` — FSM derivation (`deriveSessionSummaryUiState`)
- `apps/web/src/lib/session-summary/tie-groups.ts` — `computeTieGroups` + `hashToHsl`
- `apps/web/src/lib/session-summary/types.ts` — `SessionSummaryComposed`, `RankedParticipant`, `AchievementDto`, `ChatHighlight`, `DiaryEvent`, `DiaryTurn`
- `apps/web/src/lib/session-summary/visual-test-fixture.ts` — sentinel pattern with 6 fixture variants (default, tied, abandoned, solo, empty-achievements, empty-photos)
- `apps/web/src/lib/session-summary/state-override.ts` — `STATE_OVERRIDE_ENABLED` gate + `parseFixtureOverride`
- `apps/web/src/hooks/queries/useSessionAchievements.ts` — STUB hook returning fixture
- `apps/web/src/locales/it.json` + `en.json` — `pages.sessionSummary.*` keys (~40 keys/locale)
- Override `useSessionDetail` to support `summaryMode: true` (1h staleTime)

**Tests**: ~36 unit tests (FSM 10 + tie-groups 12 + hashToHsl 4 + parseFixture 6 + stub 4).

### Task 2: 11 v2 components (Day 3-4)

**Scope**: implement 11 components in `apps/web/src/components/v2/session-summary/`.

**Deliverables**:
- `SummaryHeroPodium.tsx` (replace stub)
- `Confetti.tsx` (NEW + `confetti.module.css`)
- `ConnectionBar.tsx` (NEW)
- `SessionKpiGrid.tsx` (replace stub)
- `ScoringBreakdownTable.tsx` (replace stub)
- `AchievementsCarousel.tsx` (NEW)
- `EmptyAchievements.tsx` (NEW — partial state placeholder)
- `SessionDiaryTimeline.tsx` (replace stub)
- `PhotosGallery.tsx` (replace stub)
- `EmptyPhotos.tsx` (NEW — partial state placeholder)
- `ChatHighlights.tsx` (NEW)
- `EmptyChat.tsx` (NEW — partial state placeholder)
- `SessionShareCard.tsx` (replace stub)
- `PlayAgainCta.tsx` (NEW)
- `index.ts` (barrel exports)

**Tests**: ~50 unit tests (component shape per state).

### Task 3: Orchestrator + page wiring (Day 5-6)

**Scope**: `SessionSummaryView.tsx` + page.tsx fork logic.

**Deliverables**:
- `apps/web/src/app/(authenticated)/sessions/[id]/_components/SessionSummaryView.tsx` — orchestrator with 4-hook composition + URL state SSOT + 6-cell FSM rendering
- `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx` — server-side fork:
  ```tsx
  export default async function Page({ params }: { params: { id: string } }) {
    const session = await api.sessions.getById(params.id);
    if (!session) notFound();
    if (session.status === 'InProgress' || session.status === 'Paused') {
      redirect(`/sessions/${params.id}/live`);
    }
    // status === 'Completed' | 'Abandoned' | 'Setup' → render summary
    return <SessionSummaryView sessionId={params.id} />;
  }
  ```
- Wire `useCompleteGameNight` to `PlayAgainCta`
- ShareCard handlers wired (Web Share API + clipboard)

**Tests**: ~28 integration tests (cell × hook composition matrix).

### Task 4: E2E specs (Day 7)

**Scope**: 4 E2E spec files.

**Deliverables**:
- `apps/web/e2e/visual-migrated/sp4-session-summary.spec.ts` — 6 visual baselines (with confetti mask)
- `apps/web/e2e/v2-states/session-summary.spec.ts` — Cell 1/3/4/6 deterministic states
- `apps/web/e2e/a11y/session-summary.spec.ts` — axe-core + reduced-motion + tied aria + diary filter keyboard
- `apps/web/e2e/smoke-real-backend/session-summary.smoke.spec.ts` — staging smoke (optional)

**Triple auth helper** in all specs: `seedAuthSession + seedCookieConsent + mockAuthEndpoints`.

### Task 5: Visual baselines bootstrap + matrix update + PR (Day 8)

**Scope**: bootstrap baselines + finalize PR.

**Deliverables**:
- `gh workflow run visual-regression-migrated.yml -f mode=bootstrap`
- Wait for 6 PNG committed to branch
- Verify 2nd run `mode=verify` passes
- Update `docs/frontend/v2-migration-matrix.md` row D.3: Status → MERGED, PR → #TBD
- Open PR with body `Closes #<D.3-issue>` + per-Gate A/B/C/D checkboxes
- Wave D total bundle delta tracking comment in PR body

## 17. Acceptance criteria (DoD checkboxes)

- [ ] **AC1** — Route `/sessions/[id]` correctly forks: `Completed/Abandoned` → summary, `InProgress/Paused` → redirect to `/live`, invalid id → 404
- [ ] **AC2** — All 6 FSM cells (loading/error/not-found/not-completed/default/partial) render correctly with appropriate UI
- [ ] **AC3** — Tie-group computation passes all 6 Gherkin scenarios (Alphabetical tie-breaker, accent-insensitive locale)
- [ ] **AC4** — Confetti respects `prefers-reduced-motion: reduce` (no animation, static medal fallback rendered)
- [ ] **AC5** — Confetti first-load only (sessionStorage flag prevents re-trigger on tab switch)
- [ ] **AC6** — ShareCard light/dark preview toggle works WITHOUT changing page theme (component-isolated)
- [ ] **AC7** — ShareCard "Download PNG" button DISABLED + tooltip "Coming soon" (out-of-scope discipline)
- [ ] **AC8** — All 4 audit gates checklist items completed (Gate A ICU plural, Gate B schema reality, Gate C MeepleCard divergence justified, Gate D bootstrap-then-merge)
- [ ] **AC9** — 6 visual baselines committed and verified (confetti masked)
- [ ] **AC10** — Bundle delta ≤ +90 KB (target ~85 KB; total wave D ≤ 280 KB tracked, mitigation noted if over 250 KB DoD)
- [ ] **AC11** — A11y E2E passes axe-core WCAG 2.1 AA (zero violations) + reduced-motion + tied podium aria announcements + diary filter keyboard nav (`useTablistKeyboardNav` reuse)
- [ ] **AC12** — i18n keys symmetric in `it.json` + `en.json` (~40 keys each, ICU plural where needed)

## 18. Open questions (deferred to implementation)

- [ ] **Q1**: `status === 'Setup'` handling — fork to `/live` (live page handles setup state) or 404? Recommendation: redirect to `/live` (consistent with InProgress/Paused, single not-completed branch).
- [ ] **Q2**: Wave D total bundle delta currently estimated ~280 KB vs DoD 250 KB. Mitigation:
  - Option A: Code-split SessionShareCard via React.lazy (only used after scroll, saves ~8 KB initial)
  - Option B: Code-split AchievementsCarousel (rarely scrolled to on mobile, saves ~5 KB)
  - Option C: Accept 30 KB overage and note in retro doc
  - **Recommendation**: Option A + B for ~13 KB savings → final ~267 KB. Document overage in PR body.
- [ ] **Q3**: ChatHighlights stub fixture — backend epic exists (TBD)? Or v1 stays stub indefinitely? Recommendation: keep stub-only, file follow-up issue for backend.
- [ ] **Q4**: PlayAgainCta "Riavvia con stessi player" — does `useCompleteGameNight.mutate` create a new session and redirect to `/sessions/<new>/live`? Or is mutation purpose different? **Pre-impl verification needed**.
- [ ] **Q5**: ShareCard share handlers fallback strategy — if `navigator.share` unavailable AND `navigator.clipboard` unavailable (older browser), graceful degrade? Recommendation: show inline modal with copy-paste textarea.

## 19. Coexistence flag

**Decisione**: NO flag (mirror Wave C.1/C.2/B.3/Wave 3/D.1/D.2 — app pre-prod, big-bang fork accettabile post-Phase 0.5).

Rollback path: `git revert` PR squash commit. `next.config.js` non ha redirect su `/sessions/[id]` (verified — see §13 audit checklist).

## 20. References

- Phase 0.5 contract Wave C.1 (template — 4-hook composition): `docs/frontend/contracts/games-id-hooks.md`
- Phase 0.5 contract Wave C.2 (template — variant matrix): `docs/frontend/contracts/agents-id-hooks.md`
- Phase 0.5 contract Wave 3 (template — index page + drawer): `docs/frontend/contracts/game-nights-hooks.md`
- Phase 0.5 contract Wave D.2 (template — most recent, dialog focus): `docs/frontend/contracts/sessions-id-live-hooks.md`
- Spec V2 migration §5 (Wave D scope): `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Spec V2 phase 1 execution §6: `docs/superpowers/specs/2026-04-27-v2-migration-phase1-execution.md`
- **Spec-panel review §3.3 (D.3 specifics) + §10 amendments**: `docs/superpowers/specs/2026-05-05-wave-d-spec-panel-review.md`
- Issue #582 (Wave D umbrella sessions triade)
- PR #736 (Wave D.1 `/sessions` Tier S blueprint — schema reality v1 carryover pattern)
- PR #741 (post-D.1 amendments + 4 audit gates)
- PR #749 + #753 (Wave D.2 Foundation + Interactions — pattern blueprint)
- Mockup source: `admin-mockups/design_files/sp4-session-summary.{html,jsx}` + `sp4-session-summary-parts.{html,jsx}`
- Stub directory: `apps/web/src/components/v2/session-summary/` (6 stubs ready: Hero, KpiGrid, ScoringBreakdownTable, SessionDiaryTimeline, PhotosGallery, ShareCard)
- Backend session DTO: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/`
- Existing hooks: `apps/web/src/hooks/queries/useSessionDetail.ts`, `useSessionFlow.ts` (`useSessionDiaryQuery`, `useCompleteGameNight`), `useSessionSnapshots.ts`
- ParticipantDto schema: `apps/web/src/lib/api/schemas/session-tracking.schemas.ts:14`
- Memory feedback files (lessons applicable to D.3):
  - `feedback_v2-tier-dispatch-strategy.md` (Tier classification)
  - `feedback_brownfield-route-redirect-audit.md` (redirect cleanup — verify no stale `/sessions/[id]` redirect)
  - `feedback_subagent-serial-only.md` (no parallel dispatch)
  - `session_2026-05-06_wave-d2-end-to-end.md` (D.2 lessons sustained)
- Pattern parents: PR #702 (Wave C.1), PR #711 (Wave C.2), PR #717 (Wave 4 D1), PR #736 (Wave D.1), PR #749/#753 (Wave D.2)

---

**Status**: DRAFT — pending review before D.3 single-shot dispatch.

**Next steps post-approval**:
1. Open child issue D.3 under umbrella #582 (route `/sessions/[id]`, Tier M-L, single-shot dispatch with this contract as task brief)
2. Create branch `feature/issue-<D.3-id>-sessions-id-summary-fe-v2` from `main-dev`
3. Pre-impl: run all 4 audit gates (Gate A grep, Gate B backend schema audit, Gate C divergence justification documented, Gate D workflow ready)
4. **Single-shot dispatch** implementation subagent referencing this contract + 5-task TDD breakdown (§16) + 6-cell FSM (§2) + 11 components (§5) + tie-group spec (§12) + Confetti contract (§9) + ShareCard scope (§10) + 17 AC checkboxes (§17)
5. Subagent two-stage review: spec compliance (this contract §17) + code quality (Wave D.2 lessons sustained)
6. After D.3 ships → close umbrella #582 + finalize Phase 3 retro doc (`docs/frontend/v2-migration-phase1-2-retro.md`)
