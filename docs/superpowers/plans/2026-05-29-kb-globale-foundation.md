# Plan — `/knowledge-base/global` Phase 1 Foundation (Issue #1482)

> **Pipeline**: `/sc:spec-panel` (2026-05-29) → this plan → `subagent-driven-development`.
> **Branch**: `feature/issue-1482-foundation` (parent: `main-dev`).
> **Worktree**: `.claude/worktrees/issue-1482-foundation` (origin/main-dev fresh, post-#1681).
> **Contract**: [`docs/for-developers/frontend/contracts/kb-globale-hooks.md`](../../for-developers/frontend/contracts/kb-globale-hooks.md) §11 (scope congelato).
> **BE follow-up issues**: #1686 (server-side facets — unblocks FilterAccordion), #1687 (PATCH metadata — unblocks editor).

## Scope (frozen)

5 componenti + 1 hook + Zod schemas + route shell + integration tests + matrix update. **Tier M effettivo** (ridotto dal Tier L del contract perché FilterAccordion + editor sono deferred).

**Estimated effort**: ~6–8h subagent time. **One PR**: `feat(kb-globale): #1482 Phase 1 Foundation`.

## Reuse vs greenfield (verified P74)

| Item | Verdict | Path |
|---|---|---|
| `useUserKbDocs(state='ready', sortBy='recent')` | ✅ REUSE (post #1645) | `apps/web/src/hooks/queries/useUserKbDocs.ts` |
| `UserKbDocDto` + `KbDocsListResponse` Zod | ✅ REUSE | `apps/web/src/lib/api/schemas/kb-docs.schemas.ts` |
| `kbDocsClient.listUserKbDocs(...)` | ✅ REUSE | `apps/web/src/lib/api/clients/kbDocsClient.ts` |
| `useGlobalKbSearch` | ❌ GREENFIELD | `apps/web/src/hooks/queries/useGlobalKbSearch.ts` |
| `kbClient.searchGlobal(req)` | ❌ GREENFIELD | extend `apps/web/src/lib/api/clients/kbDocsClient.ts` (or new `kbGlobaleClient.ts`) |
| `kb-globale.schemas.ts` | ❌ GREENFIELD | `apps/web/src/lib/api/schemas/kb-globale.schemas.ts` |
| 5 components | ❌ GREENFIELD | `apps/web/src/components/features/kb-globale/*` |
| Route shell | ❌ GREENFIELD | `apps/web/src/app/(authenticated)/knowledge-base/global/page.tsx` + `_components/KbGlobaleView.tsx` |

## BE contract recap (verified against origin/main-dev `1111e4dc5`)

```http
POST /api/v1/knowledge-base/search/global
Body: { Query: string, Limit?: int=20, Cursor?: string|null, Mode?: SearchMode|null, MinScore?: double|null }
Resp: {
  Results: [{
    ChunkId: string,
    DocId: Guid,
    DocTitle: string,
    GameId: Guid,
    GameName: string,
    DocType: string,
    HeadingPath: string|null,
    Snippet: string,
    PageNumber: int|null,
    Score: float
  }],
  HasMore: bool,
  NextCursor: string|null
}
```

`SearchMode` enum: at least `Semantic` (default). FE exposes only `Semantic` in v1; other modes TBD via Q6 follow-up if surfaced.

## Tasks (TDD red→green, sequential dispatch)

> **Subagent dispatch convention**: model hint follows P120 (mix-on-complexity). Each task = one subagent invocation in `feature-dev:code-architect` or `coder` agent, depending on layering. RED→GREEN→commit per task.

### Task 0 — Zod schemas + client method (haiku · ~30min)

**Files**:
- NEW `apps/web/src/lib/api/schemas/kb-globale.schemas.ts`
- EDIT `apps/web/src/lib/api/clients/kbDocsClient.ts` (or NEW `kbGlobaleClient.ts` if cleaner) — add `searchGlobal(req): Promise<GlobalKbSearchResponse>`
- NEW `apps/web/src/lib/api/schemas/__tests__/kb-globale.schemas.test.ts`

**Schema spec** (mirrors §11 of contract):

```ts
export const SearchModeSchema = z.enum(['Semantic']); // expand as BE adds modes
export const GlobalKbSearchRequestSchema = z.object({
  query: z.string().min(1),
  limit: z.number().int().positive().optional(),
  cursor: z.string().nullable().optional(),
  mode: SearchModeSchema.optional(),
});
export const GlobalKbSearchResultSchema = z.object({
  chunkId: z.string(),
  docId: z.string().uuid(),
  docTitle: z.string(),
  gameId: z.string().uuid(),
  gameName: z.string(),
  docType: z.string(),
  headingPath: z.string().nullable(),
  snippet: z.string(),
  pageNumber: z.number().int().nullable(),
  score: z.number(),
});
export const GlobalKbSearchResponseSchema = z.object({
  results: z.array(GlobalKbSearchResultSchema),
  hasMore: z.boolean(),
  nextCursor: z.string().nullable(),
}).strict();
```

**AC (Task 0)**:
- [ ] Schema match shipped BE DTO 1:1 (Results/HasMore/NextCursor field names; result has 10 fields)
- [ ] `searchGlobal({query, limit?, cursor?, mode?})` resolves to parsed response (HttpClient + Zod validation via existing client infra)
- [ ] Unit tests: valid request roundtrip, optional fields default, rejects unknown fields (`.strict()`), rejects bad enum, rejects malformed UUID/score
- [ ] `pnpm typecheck` + `pnpm lint` clean

**Depends on**: nothing.

### Task 1 — `useGlobalKbSearch` hook (sonnet · ~1h)

**Files**:
- NEW `apps/web/src/hooks/queries/useGlobalKbSearch.ts`
- NEW `apps/web/src/hooks/queries/__tests__/useGlobalKbSearch.test.tsx`

**Hook spec**:

```ts
export interface UseGlobalKbSearchOptions {
  query: string;
  mode?: SearchMode;
  debounceMs?: number; // default 250
  enabled?: boolean;   // default !!query
}
export interface UseGlobalKbSearchResult {
  data: { results: readonly GlobalKbSearchResult[]; hasMore: boolean } | undefined;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: Error | null;
  fetchNextPage: () => void; // appends cursor page
  resetCursor: () => void;   // on query/mode change
}
export function useGlobalKbSearch(opts: UseGlobalKbSearchOptions): UseGlobalKbSearchResult;
```

**Implementation hints**:
- Use `useInfiniteQuery` from React Query keyed on `['kb-globale', 'search', { query, mode }]`.
- `getNextPageParam: lastPage => lastPage.hasMore ? lastPage.nextCursor : undefined`.
- Debounce query string before key change (250ms default). Reuse existing debounce primitive in tree if any; otherwise inline `useDebouncedValue` (small, no new dep).
- `enabled = !!query && query.length >= 2` to avoid 1-char noise (Wiegers: testable threshold).
- `staleTime: 5 * 60 * 1000` (search results valid 5min, mirrors useUserKbDocs).

**AC (Task 1)**:
- [ ] Query `""` → `enabled=false`, no fetch, `data=undefined`
- [ ] Query change → cursor reset, refetch (no leak from previous query into `data.results`)
- [ ] `fetchNextPage()` appends next page; `hasMore` aggregates last page only
- [ ] Mode change behaves like query change (full reset)
- [ ] Error from client surfaces as `error`, no thrown
- [ ] Debounce: rapid typing fires only the final request (use vi.useFakeTimers)
- [ ] Unit tests cover: 7 scenarios above + mocked client via `vi.mock('@/lib/api')` pattern

**Depends on**: Task 0.

### Task 2 — `HeroSearch` component + `SearchMode` toggle (haiku · ~45min)

**Files**:
- NEW `apps/web/src/components/features/kb-globale/HeroSearch.tsx`
- NEW `apps/web/src/components/features/kb-globale/__tests__/HeroSearch.test.tsx`

**Props**:

```ts
interface HeroSearchProps {
  initialQuery?: string;
  initialMode?: SearchMode;
  onSubmit: (q: string, mode: SearchMode) => void; // pushes to URL
  onClear?: () => void;
  labels: {
    placeholder: string;
    submit: string;
    modeLabel: string;
    modeOptions: Record<SearchMode, string>;
  };
}
```

**Rendering**: large hero with text input + segmented control for `mode` (Semantic default) + submit button (or Enter key). Clear button when value present. DS-15 tokens (`bg-card`, `text-foreground`, `border-border`, `bg-entity-game` for accent if alignment with KB theme — verify against mockup `sp4-kb-globale.html`).

**AC (Task 2)**:
- [ ] Render input with placeholder from labels
- [ ] Segmented control reflects mode, calls onSubmit with current value on Enter/click submit
- [ ] Clear button (×) when value non-empty, calls onClear and clears input
- [ ] Labels-injected (no hardcoded strings — i18n consumer wires them)
- [ ] `jest-axe` test passes (no a11y violations: input has label, buttons have accessible names)
- [ ] No-token violations: `pnpm lint:tokens`

**Depends on**: Task 0 (schema types).

### Task 3 — `KbEmptyState` (haiku · ~30min)

**Files**:
- NEW `apps/web/src/components/features/kb-globale/KbEmptyState.tsx`
- NEW `apps/web/src/components/features/kb-globale/__tests__/KbEmptyState.test.tsx`

**Props**:

```ts
type KbEmptyKind = 'no-query' | 'no-results';
interface KbEmptyStateProps {
  kind: KbEmptyKind;
  labels: { title: string; description: string; cta?: string };
  onCtaClick?: () => void;
}
```

**Rendering**: discriminated render per `kind`. `no-query` is landing CTA "Start searching across all your games". `no-results` is "No matches for «query» — try a different mode or simpler terms".

**AC (Task 3)**:
- [ ] Renders title + description per kind
- [ ] CTA button rendered when `cta` label provided, calls onCtaClick
- [ ] jest-axe clean (role="status" or descriptive heading hierarchy)
- [ ] DS-15 tokens

**Depends on**: nothing.

### Task 4 — `KbHomeDesktop` landing (sonnet · ~1h)

**Files**:
- NEW `apps/web/src/components/features/kb-globale/KbHomeDesktop.tsx`
- NEW `apps/web/src/components/features/kb-globale/__tests__/KbHomeDesktop.test.tsx`

**Spec**: rendered when URL has no `?q=`. Shows recent KB docs cross-game (via `useUserKbDocs({state: 'ready', sortBy: 'recent', pageSize: 12})` reuse — post-#1645 API).

**Props**:

```ts
interface KbHomeDesktopProps {
  // hook results passed in (testability — facility orchestrator-level mocking)
  recentDocs: UserKbDoc[];
  isLoading: boolean;
  error: Error | null;
  labels: { /* heading, empty CTA, doc card aria, ... */ };
  onDocClick?: (docId: string) => void; // Phase 2 viewer; in Foundation = TBD/optional
}
```

Inline doc card list (12 items max, 3-col grid desktop). Loading state = 12 skeleton cards. Error → inline error banner. Empty → `<KbEmptyState kind="no-query" />`.

**AC (Task 4)**:
- [ ] Renders 12 doc cards with title + gameName + processedAt-as-updatedAt
- [ ] Loading shows 12 skeletons (data-testid)
- [ ] Error renders error banner (Nygard: no swallowed errors)
- [ ] Empty (`recentDocs.length === 0 && !isLoading && !error`) renders KbEmptyState
- [ ] No waterfall: hook is consumed by orchestrator (Task 6), KbHomeDesktop is pure
- [ ] jest-axe + DS-15

**Depends on**: Task 3 (KbEmptyState).

### Task 5 — `KbSearchResultsDesktop` + load-more (sonnet · ~1.5h)

**Files**:
- NEW `apps/web/src/components/features/kb-globale/KbSearchResultsDesktop.tsx`
- NEW `apps/web/src/components/features/kb-globale/__tests__/KbSearchResultsDesktop.test.tsx`

**Props**:

```ts
interface KbSearchResultsDesktopProps {
  results: readonly GlobalKbSearchResult[];
  hasMore: boolean;
  isLoading: boolean;
  isFetchingNextPage: boolean;
  error: Error | null;
  onLoadMore: () => void;
  onResultClick?: (r: GlobalKbSearchResult) => void; // Phase 2 viewer; in Foundation: TBD
  labels: { /* loadMore, loadingMore, empty, error, headerCount, ... */ };
}
```

Result row layout (per mockup `sp4-kb-globale-search.html` — verify exact layout when implementing):
- `gameName · docTitle · DocType pill · pageNumber?`
- `Snippet` (highlight match if feasible — Phase 2 nice-to-have, **NOT** Foundation AC)
- `headingPath` line if non-null
- score badge (small, muted)

Load-more button at bottom when `hasMore`. Disabled + spinner when `isFetchingNextPage`. Empty → `KbEmptyState kind="no-results"`.

**AC (Task 5)**:
- [ ] Renders N results with all 10 fields visible (score visible but muted)
- [ ] `hasMore=true` → load-more button rendered + clickable
- [ ] `isFetchingNextPage=true` → button disabled with spinner label
- [ ] `results=[]` + `!isLoading` + `!error` → KbEmptyState kind="no-results"
- [ ] `error` → error banner, no result list
- [ ] No infinite-scroll auto-trigger in v1 (explicit click — Nygard: predictability)
- [ ] jest-axe + DS-15

**Depends on**: Task 0 (schema types), Task 3 (KbEmptyState).

### Task 6 — Route shell + orchestrator (sonnet · ~1.5h)

**Files**:
- NEW `apps/web/src/app/(authenticated)/knowledge-base/global/page.tsx` (server component thin wrapper)
- NEW `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx` (client orchestrator)
- NEW `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx`

**Server page** (Next.js App Router):

```tsx
// page.tsx
import { Suspense } from 'react';
import { KbGlobaleView } from './_components/KbGlobaleView';

export default function Page() {
  return (
    <Suspense fallback={<KbGlobaleSkeleton />}>
      <KbGlobaleView />
    </Suspense>
  );
}
```

**Orchestrator** (URL SSOT):
- Read `searchParams` (or `useSearchParams` client-side): `q`, `mode`.
- If `!q` → render `<KbHomeDesktop>` (consume `useUserKbDocs(...)`).
- If `q` → render `<KbSearchResultsDesktop>` (consume `useGlobalKbSearch({query: q, mode})`).
- Render `<HeroSearch initialQuery={q} initialMode={mode} onSubmit={pushUrl} onClear={clearUrl}>` always on top.
- `pushUrl(q, mode)` uses `useRouter().push` or `useSearchParams.set` — match existing pattern in `/discover` route.

**AC (Task 6)**:
- [ ] Route renders at `/knowledge-base/global` (HTTP 200, no SSR error)
- [ ] `?q=azul` → results view; `?q=&mode=Semantic` → home (empty q)
- [ ] HeroSearch submit pushes URL (test via mock `useRouter` or `URLSearchParams` assertion)
- [ ] Home vs Results branch is testable via setup (mock useSearchParams)
- [ ] No double-fetch: orchestrator decides which hook fires per URL state (no `enabled: true` for both)
- [ ] jest-axe clean

**Depends on**: Tasks 1, 2, 4, 5.

### Task 7 — Integration test (MSW) (sonnet · ~1h)

**Files**:
- NEW `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.integration.test.tsx`

**Scenarios** (MSW-mocked `POST /api/v1/knowledge-base/search/global` + `GET /api/v1/kb-docs`):
1. Land on `/knowledge-base/global` with no `q` → home renders 12 recent docs.
2. Type "azul" + submit → URL changes to `?q=azul`, results render N rows.
3. Click "Load more" → next page appended (cursor passed correctly).
4. Mode toggle to Semantic (default already) → no-op refetch (already-fetched data reused).
5. Server returns 500 → error banner visible, no crash.
6. Query "" via clear → back to home view.

**AC (Task 7)**:
- [ ] 6 scenarios covered, all pass
- [ ] MSW handlers assert request shape (Zod-validated body)
- [ ] No `act()` warnings, no console errors

**Depends on**: Task 6.

### Task 8 — Matrix + bundle budget + i18n (haiku · ~30min)

**Files**:
- EDIT `docs/for-developers/frontend/v2-migration-matrix.md` — add 5 rows for kb-globale components + 1 row for route, status `done`, link PR.
- EDIT (if exists) `apps/web/.bundle-budgets.json` — add entry for `/knowledge-base/global` with budget ~+40 KB (Foundation).
- EDIT `apps/web/src/i18n/messages/it.json` + `en.json` — add labels for HeroSearch, KbEmptyState, KbHomeDesktop, KbSearchResultsDesktop (key namespace `pages.kbGlobale.*`).
- EDIT `docs/for-developers/audits/2026-05-22-mockup-gaps.md` (if entry exists for kb-globale) — flip to closed.

**AC (Task 8)**:
- [ ] Matrix shows 6 rows under "kb-globale" with `Status=done`, `PR=#<this PR>`
- [ ] Bundle budget entry added (or skipped if file absent — document in PR body)
- [ ] i18n keys present in both `it.json` and `en.json`, used by Tasks 2-5 (no untranslated leaf strings)

**Depends on**: Task 7 (closes the loop).

## Final review checklist (pre-PR)

- [ ] `pnpm typecheck` (worktree) clean
- [ ] `pnpm lint` clean
- [ ] `pnpm lint:tokens` clean
- [ ] `pnpm test -- kb-globale useGlobalKbSearch kb-globale.schemas` — all green
- [ ] `superpowers:code-reviewer` agent final pass (APPROVED or APPROVED_WITH_NOTES)
- [ ] Branch is `feature/issue-1482-foundation` ← parent `main-dev`
- [ ] Commit messages follow conventional commits (`feat(kb-globale): #1482 ...`)

## PR

- **Title**: `feat(kb-globale): #1482 Phase 1 Foundation — search + landing + 5 components`
- **Base**: `main-dev`
- **Body must contain**:
  - `Refs #1482` (NOT `Closes` — Phase 2 still pending)
  - Link to contract §11
  - Link to BE follow-up issues #1686 / #1687 (in "Deferred" section)
  - Test counts (BE+FE)
  - Bundle delta vs baseline (target ≤ +40 KB)

## Out of scope (explicit — for reviewer clarity)

- `FilterAccordion` (component #4) — deferred to #1686
- `KbDocViewerDesktop` / `KbDocViewerMobile` — Phase 2
- `KbEditorDesktop` — deferred to #1687
- `DrawerShell` + AI ask + `useKbAskStream` — Phase 2
- `CitationPill` — Phase 2
- Mobile-specific KB home/search variants — Phase 2 (mobile drawer pattern)
- Snippet highlighting / match-highlight — Phase 2 nice-to-have
- Infinite scroll — v1 keeps explicit "Load more" (Nygard predictability)
