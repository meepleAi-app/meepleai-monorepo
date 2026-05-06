# V2 Migration · Wave A.4 — `/shared-games/[id]` Brownfield Spec

**Issue**: #603 (parent: #579 Wave A umbrella)
**Branch**: `feature/issue-603-shared-game-detail-fe-v2` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp3-shared-game-detail.jsx` (1116 LOC)
**Visual baseline mockup**: `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-shared-game-detail-mockup-baseline-{desktop,mobile}-linux.png` (PR #575 Phase 0)
**Sequential prerequisites**: A.1 #583 ✅ · A.2 #589 ✅ · A.3a #593 ✅ · A.3b #596 ✅ (PR #600 squash `86051704e` MERGED 2026-04-28)
**A11y prerequisite**: PR #602 (`frontend-a11y` axe-core CI gate, in progress) — eredita evidenza diagnostica per #587
**Issue absorbed**: #588 (CategoryTabs Arrow keys a11y) — risolta dal nuovo `Tabs` v2 component WAI-ARIA tablist
**Pilot reference**: `2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md` (PR #600 MERGED)
**Status**: DRAFT 2026-04-28 — pending kickoff

---

## 1. Goals

1. **Migrare brownfield** `/shared-games/[id]` route esistente (`apps/web/src/app/(public)/shared-games/[id]/page.tsx` 120 LOC) al design v2 mockup `sp3-shared-game-detail` 1:1.
2. **Estendere backend `SharedGameDetailDto`** con aggregate counts/flags + 3 nested public lists (Opzione B confermata) per coprire il dettaglio mockup completo in single SSR fetch.
3. **Implementare WAI-ARIA tablist** completo nel nuovo `Tabs` v2 component (assorbe issue #588 — chiude implicitamente con AC equivalente).
4. **Riusare 1:1 da prod**: `ContributorsSection` (`apps/web/src/components/shared-games/ContributorsSection.tsx`, PR #549/#552), `ConnectionBar` (PR #549/#552). Reuse da Wave A.3b: `MeepleCardGame`, helper `entityHsl()`, hook `useUrlHashState`, pattern bootstrap baselines.
5. **Deprecare `SharedGameDetailModal.tsx`** (`apps/web/src/components/shared-games/SharedGameDetailModal.tsx`) — la nuova route è la canonical surface per il dettaglio gioco pubblico.
6. **Mantenere performance gates**: p95 cold-cache <250ms / warm <50ms su `GET /shared-games/{id}` (single HybridCache entry).

## 2. Non-goals

- Refactor di `/shared-games/[id]/admin` (route admin separata, rimane v1).
- Pagination delle nested list (toolkits/agents/kbs) — Alpha: top-N inline, sufficiente per ≤ 10 items per categoria.
- Tab body lazy-load via Suspense — tutti i tab body renderano al mount con `aria-hidden` toggle (mockup non differenzia).
- Real-time SSE per contributors/aggregates (post-Alpha).
- Edit/delete user-generated content (toolkit/agent submission flow è fuori Wave A).
- Mobile drawer per "tab overflow" (≥ 4 tab visibili tutti su mobile via horizontal scroll del tablist con momentum).
- BGG live fetch (catalogo è community DB).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| **Backend DTO** | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs` (record `SharedGameDetailDto`) | **Edit** (+7 fields + 3 nested DTOs) |
| **Backend DTO new** | stesso file: `record PublishedToolkitPreviewDto`, `record PublishedAgentPreviewDto`, `record PublishedKbPreviewDto` | **Create** (3 sealed records) |
| **Backend handler** | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandler.cs` | **Edit** (cross-BC LINQ projection con filter status/soft-delete) |
| **Backend invalidation** | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/ToolkitChangedForCatalogAggregatesHandler.cs` | **Edit** (aggiungi tag `shared-game:{id}` da invalidare) |
| **Backend invalidation** | stesso dir: `AgentDefinitionChangedForCatalogAggregatesHandler.cs` | **Edit** (idem) |
| **Backend invalidation** | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/VectorDocumentIndexedForKbFlagHandler.cs` (legacy) | **Edit** (idem — invalida `shared-game:{SharedGameId}` quando match) |
| **Backend test** | `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandlerTests.cs` | **Edit** (assert nested DTOs projection + flag computation) |
| **Backend test new** | stesso dir: `GetSharedGameByIdQueryHandlerIntegrationTests.cs` | **Create** (Testcontainers cross-BC join) |
| **Backend test** | `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/*HandlerTests.cs` (3 file) | **Edit** (assert tag eviction `shared-game:{id}` con real-cache-from-DI pattern) |
| Server page | `apps/web/src/app/(public)/shared-games/[id]/page.tsx` | **Rewrite** (server component, `generateMetadata`, SSR 2-way `Promise.allSettled`) |
| Client page | `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx` | **Create** (state + tabs + sticky CTA orchestration) |
| Page test | `apps/web/src/app/(public)/shared-games/[id]/__tests__/page-client.test.tsx` | **Create** |
| Submit/fetch hook | `apps/web/src/hooks/useSharedGameDetail.ts` | **Create** (TanStack Query SSR seed) |
| API client | `apps/web/src/lib/api/shared-games.ts` (esistente A.3b) | **Edit** (aggiungi `getSharedGameDetail(id)` + tipi `SharedGameDetailV2`) |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/hero.tsx` | **Create** (entity-game tinta orange, image + metadata + ConnectionBar 1:1 prod) |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/tabs.tsx` | **Create** (WAI-ARIA tablist, roving tabindex, ArrowLeft/Right wrap, Home/End — chiude #588) |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/toolkit-list-item.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/agent-list-item.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/kb-doc-item.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/contributors-strip.tsx` | **Create** (8 avatars con entity tinta) |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/sticky-cta.tsx` | **Create** ("Accedi per installare", sticky-bottom mobile / floating bottom-right desktop) |
| Component | `apps/web/src/components/ui/v2/shared-game-detail/empty-state.tsx` | **Create** (kind: `'no-toolkits' \| 'no-agents' \| 'no-kbs'`) |
| Component index | `apps/web/src/components/ui/v2/shared-game-detail/index.ts` | **Create** |
| **Reuse 1:1 prod** | `apps/web/src/components/shared-games/ContributorsSection.tsx` | **Reuse as-is** (PR #549/#552) |
| **Reuse 1:1 prod** | `ConnectionBar` (path TBD da PR #549/#552) | **Reuse as-is** |
| **Reuse A.3b** | `apps/web/src/hooks/useUrlHashState.ts` (per tab state persistence) | **Reuse** |
| **Reuse A.3b** | `apps/web/src/components/ui/v2/shared-games/meeple-card-game.tsx` | **Reuse** (per related games suggestions se mockup li ha) |
| **Reuse Phase 0** | `apps/web/src/lib/utils/entity-hsl.ts` (helper `entityHsl()`) | **Reuse** |
| **Deprecation** | `apps/web/src/components/shared-games/SharedGameDetailModal.tsx` | **Mark `@deprecated`** in JSDoc + audit call sites |
| **Deprecation** | `apps/web/src/components/shared-games/index.ts` | **Edit** (rimuovi export se 0 call sites residui) |
| i18n IT | `apps/web/src/locales/it.json` § `pages.sharedGameDetail` | **Add** (~30 keys) |
| i18n EN | `apps/web/src/locales/en.json` § `pages.sharedGameDetail` | **Add** (~30 keys) |
| Visual test | `apps/web/e2e/visual-migrated/sp3-shared-game-detail.spec.ts` | **Create** (1 desktop + 1 mobile) |
| State test | `apps/web/e2e/v2-states/shared-game-detail.spec.ts` | **Create** (5 stati × 2 viewport = 10 PNG) |
| Baselines | `apps/web/e2e/visual-migrated/sp3-shared-game-detail.spec.ts-snapshots/*.png` | **Create** (2 PNG via CI bootstrap) |
| Baselines | `apps/web/e2e/v2-states/shared-game-detail.spec.ts-snapshots/*.png` | **Create** (10 PNG via CI bootstrap) |

### 3.2 Backend extension — `SharedGameDetailDto` shape

```csharp
// Nested DTOs (3 new sealed records in SharedGameDto.cs)
public sealed record PublishedToolkitPreviewDto(
    Guid Id,
    string Name,
    string Version,
    Guid OwnerId,
    string OwnerName,
    int DownloadCount,
    DateTime LastUpdatedAt);

public sealed record PublishedAgentPreviewDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerName,
    decimal? AverageRating,
    int RatingCount);

public sealed record PublishedKbPreviewDto(
    Guid Id,
    string Title,
    string DocumentType,         // "rulebook" | "errata" | "faq" | "user-doc"
    DateTime LastUpdatedAt,
    int? PageCount);

// Extended SharedGameDetailDto (additive — defaulted to preserve back-compat)
public sealed record SharedGameDetailDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto? Rules,
    GameStatus Status,
    Guid CreatedBy,
    Guid? ModifiedBy,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<GameFaqDto> Faqs,
    IReadOnlyList<GameErrataDto> Erratas,
    IReadOnlyList<GameDesignerDto> Designers,
    IReadOnlyList<GamePublisherDto> Publishers,
    IReadOnlyList<GameCategorySimpleDto> Categories,
    IReadOnlyList<GameMechanicSimpleDto> Mechanics,
    // === A.4 extension (defaulted) ===
    IReadOnlyList<PublishedToolkitPreviewDto>? Toolkits = null,
    IReadOnlyList<PublishedAgentPreviewDto>? Agents = null,
    IReadOnlyList<PublishedKbPreviewDto>? Kbs = null,
    int ToolkitsCount = 0,
    int AgentsCount = 0,
    int KbsCount = 0,
    int ContributorsCount = 0,
    bool HasKnowledgeBase = false,
    bool IsTopRated = false,
    bool IsNew = false);
```

**Handler projection** (cross-BC, single LINQ traversal):

```csharp
// In FetchGameDetailsAsync — after existing mappings, before final return:

// Cross-BC: Game.SharedGameId == sharedGame.Id (FK chain SharedGame ← Game)
var games = await _gameRepository.GetBySharedGameIdAsync(game.Id, ct).ConfigureAwait(false);
var gameIds = games.Select(g => g.Id).ToList();

var toolkits = await _toolkitRepository
    .GetPublishedByGameIdsAsync(gameIds, ct)         // status=Published, !IsDeleted
    .ConfigureAwait(false);
var agents = await _agentDefinitionRepository
    .GetPublishedByGameIdsAsync(gameIds, ct)
    .ConfigureAwait(false);
var kbs = await _vectorDocumentRepository
    .GetIndexedBySharedGameIdAsync(game.Id, ct)      // direct FK SharedGameId on VectorDocument
    .ConfigureAwait(false);
var contributorsCount = await _contributorRepository
    .CountBySharedGameIdAsync(game.Id, ct)
    .ConfigureAwait(false);

// IsTopRated: rating ≥ TopRatedThreshold config (4.0 set in A.3b)
// IsNew: CreatedAt within NewWindowDays config (7d set in A.3a)
var topRatedThreshold = _config.GetValue<decimal>("SharedGameCatalog:TopRatedThreshold", 4.0m);
var newWindowDays = _config.GetValue<int>("SharedGameCatalog:NewWindowDays", 7);

return new SharedGameDetailDto(
    // ... existing fields ...
    Toolkits: toolkits.Select(MapToolkit).ToList(),
    Agents: agents.Select(MapAgent).ToList(),
    Kbs: kbs.Select(MapKb).ToList(),
    ToolkitsCount: toolkits.Count,
    AgentsCount: agents.Count,
    KbsCount: kbs.Count,
    ContributorsCount: contributorsCount,
    HasKnowledgeBase: kbs.Count > 0,
    IsTopRated: (game.AverageRating ?? 0) >= topRatedThreshold * 2,  // scale 0-10 = stars 0-5 × 2
    IsNew: game.CreatedAt >= DateTime.UtcNow.AddDays(-newWindowDays));
```

**Cache invalidation**:
- Cache key invariato: `shared-game:{game.Id}`
- Tag aggiunto: `shared-game:{game.Id}` (HybridCache tag, paragonabile a `search-games` tag in A.3a)
- Event handler estesi:
  - `ToolkitChangedForCatalogAggregatesHandler` — invalida `search-games` (esistente A.3a) + `shared-game:{toolkit.GameId.SharedGameId}` (nuovo)
  - `AgentDefinitionChangedForCatalogAggregatesHandler` — idem
  - `VectorDocumentIndexedForKbFlagHandler` (legacy) — già invalida tag legacy; aggiunto `shared-game:{vd.SharedGameId}` quando FK match

**Pitfall #2620 — Tag store consistency**: il producer `GetSharedGameByIdQueryHandler` usa raw `HybridCache.GetOrCreateAsync(..., tags: ["shared-game:{id}"])`. I consumer (event handlers) DEVONO iniettare raw `HybridCache` (NOT wrapper `IHybridCacheService`) per `RemoveByTagAsync`. Mirror del fix Wave A.3a (PR #594).

### 3.3 Frontend — page-client architecture

```tsx
// page.tsx (server component)
export const revalidate = 60;  // ISR aligned con HybridCache TTL

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const detail = await getSharedGameDetail(id).catch(() => null);
  if (!detail) return { title: 'Game Not Found — MeepleAI' };
  return {
    title: `${detail.title} (${detail.yearPublished}) — MeepleAI`,
    description: detail.description.slice(0, 160),
    openGraph: {
      images: [{ url: detail.imageUrl, alt: detail.title }],
    },
  };
}

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  // 2-way SSR fault-tolerant
  const [detailResult, contributorsResult] = await Promise.allSettled([
    getSharedGameDetail(id),
    getGameContributors(id),
  ]);

  const initialDetail = detailResult.status === 'fulfilled' ? detailResult.value : null;
  const initialContributors = contributorsResult.status === 'fulfilled' ? contributorsResult.value : [];

  return (
    <SharedGameDetailPageClient
      gameId={id}
      initialDetail={initialDetail}
      initialContributors={initialContributors}
    />
  );
}
```

```tsx
// page-client.tsx — 5-state surface dispatcher
'use client';

type DetailState = 'default' | 'loading' | 'error' | 'not-found' | 'empty';

export default function SharedGameDetailPageClient({
  gameId, initialDetail, initialContributors,
}: Props) {
  const detail = useSharedGameDetail(gameId, { initialData: initialDetail });
  const stateOverride = useStateOverride(); // ?state= guarded NODE_ENV !== 'production'

  // Override clears data per visual-test determinism (mirror A.3b pattern)
  const data = stateOverride && stateOverride !== 'default' ? null : detail.data;

  const effectiveState: DetailState =
    stateOverride ??
    (detail.isLoading ? 'loading'
    : detail.isError ? 'error'
    : !data ? 'not-found'
    : (data.toolkitsCount === 0 && data.agentsCount === 0 && data.kbsCount === 0) ? 'empty'
    : 'default');

  return (
    <main data-testid="shared-game-detail-page" data-state={effectiveState}>
      {effectiveState === 'loading' && <DetailSkeleton />}
      {effectiveState === 'error' && <ErrorState onRetry={detail.refetch} />}
      {effectiveState === 'not-found' && <NotFoundState gameId={gameId} />}
      {(effectiveState === 'default' || effectiveState === 'empty') && data && (
        <>
          <Hero detail={data} />
          <ContributorsStrip contributors={initialContributors} />
          <Tabs detail={data} state={effectiveState} />
          <StickyCTA detail={data} />
        </>
      )}
    </main>
  );
}
```

### 3.4 `Tabs` v2 component — WAI-ARIA tablist (closes #588)

```tsx
// tabs.tsx — implementazione richiesta:

interface TabsProps {
  readonly detail: SharedGameDetailV2;
  readonly state: 'default' | 'empty';
}

const TAB_KEYS = ['overview', 'toolkits', 'agents', 'knowledge', 'community'] as const;
type TabKey = typeof TAB_KEYS[number];

export function Tabs({ detail, state }: TabsProps): JSX.Element {
  const [activeTab, setActiveTab] = useUrlHashState<TabKey>('tab', 'overview');
  const tabRefs = useRef<Map<TabKey, HTMLButtonElement>>(new Map());

  const handleKeyDown = (e: React.KeyboardEvent, currentTab: TabKey) => {
    const idx = TAB_KEYS.indexOf(currentTab);
    let nextIdx: number | null = null;

    switch (e.key) {
      case 'ArrowLeft': nextIdx = (idx - 1 + TAB_KEYS.length) % TAB_KEYS.length; break;  // wrap
      case 'ArrowRight': nextIdx = (idx + 1) % TAB_KEYS.length; break;                    // wrap
      case 'Home': nextIdx = 0; break;
      case 'End': nextIdx = TAB_KEYS.length - 1; break;
      default: return;
    }

    e.preventDefault();
    const nextTab = TAB_KEYS[nextIdx!];
    setActiveTab(nextTab);
    tabRefs.current.get(nextTab)?.focus();  // roving tabindex: focus deve seguire selection
  };

  return (
    <div className="tabs-container">
      {/* Tablist */}
      <div role="tablist" aria-label={t('pages.sharedGameDetail.tabs.label')}>
        {TAB_KEYS.map((key) => (
          <button
            key={key}
            ref={(el) => { if (el) tabRefs.current.set(key, el); }}
            role="tab"
            id={`tab-${key}`}
            aria-selected={activeTab === key}
            aria-controls={`tabpanel-${key}`}
            tabIndex={activeTab === key ? 0 : -1}  // roving tabindex
            onClick={() => setActiveTab(key)}
            onKeyDown={(e) => handleKeyDown(e, key)}
          >
            {t(`pages.sharedGameDetail.tabs.${key}`)}
          </button>
        ))}
      </div>

      {/* Tabpanels — tutti renderati ma aria-hidden quando non attivi */}
      {TAB_KEYS.map((key) => (
        <div
          key={key}
          role="tabpanel"
          id={`tabpanel-${key}`}
          aria-labelledby={`tab-${key}`}
          aria-hidden={activeTab !== key}
          hidden={activeTab !== key}  // also visually hide
        >
          {key === 'overview' && <OverviewPanel detail={detail} />}
          {key === 'toolkits' && <ToolkitsPanel detail={detail} state={state} />}
          {key === 'agents' && <AgentsPanel detail={detail} state={state} />}
          {key === 'knowledge' && <KnowledgePanel detail={detail} state={state} />}
          {key === 'community' && <CommunityPanel detail={detail} />}
        </div>
      ))}
    </div>
  );
}
```

**A11y AC verifica** (closes #588):
- ✅ `role="tablist"` con `aria-label` localizzato
- ✅ `role="tab"` con `aria-selected={boolean}` (NOT `aria-current`)
- ✅ `role="tab"` con `aria-controls={panelId}` per associazione esplicita
- ✅ Roving tabindex: solo tab attivo `tabIndex=0`, altri `tabIndex=-1`
- ✅ ArrowLeft wrap: idx 0 → idx N-1
- ✅ ArrowRight wrap: idx N-1 → idx 0
- ✅ Home → idx 0; End → idx N-1
- ✅ Focus segue selection (`tabRefs.current.get(...)?.focus()`)
- ✅ `role="tabpanel"` con `aria-labelledby={tabId}`
- ✅ `aria-hidden={!isActive}` su tabpanel non attivo (NOT `hidden` HTML attr — sopprime transition; usa entrambi: `hidden` per layout, `aria-hidden` per AT)

## 4. Surface states (5)

| Stato | Trigger | Override `?state=` | Render |
|-------|---------|--------------------|--------|
| **default** | detail caricato, almeno 1 di {toolkits,agents,kbs} non vuoto | (unset o `default`) | Hero + ContributorsStrip + Tabs (5 panel renderati) + StickyCTA |
| **loading** | `isLoading` true (no SSR seed) | `?state=loading` | DetailSkeleton (hero shimmer + tabs shimmer + 3 list-item shimmer) |
| **error** | `isError` true (SSR `detailResult.status === 'rejected'` AND no client cache) | `?state=error` | ErrorState con retry button + i18n message |
| **not-found** | detail null (404 backend) | `?state=not-found` | NotFoundState con CTA "Torna al catalogo" + link `/shared-games` |
| **empty** | detail caricato MA `toolkitsCount === 0 && agentsCount === 0 && kbsCount === 0` | `?state=empty` | Hero + ContributorsStrip + Tabs (5 panel ma toolkits/agents/knowledge mostrano EmptyState con CTA "Sii il primo a contribuire") + StickyCTA |

**Critical pattern (mirror A.3b A.2)**: l'override `?state=...` deve **clear data** per stati non-default per garantire visual-test determinism (skeleton/error/not-found rendono unobstructed, no stale data leak).

## 5. i18n keys (~30 × 2 locales)

```yaml
pages.sharedGameDetail:
  meta:
    titleSuffix: "MeepleAI"
    notFoundTitle: "Game Not Found"
  hero:
    yearLabel: "Pubblicato"
    playersLabel: "Giocatori"
    timeLabel: "Tempo"
    complexityLabel: "Complessità"
    ratingLabel: "Valutazione"
  tabs:
    label: "Sezioni del gioco"
    overview: "Panoramica"
    toolkits: "Toolkit"
    agents: "Agenti"
    knowledge: "Knowledge"
    community: "Community"
  toolkits:
    title: "Toolkit pubblicati"
    countLabel: "{count, plural, one {# toolkit} other {# toolkit}}"
    downloadsLabel: "{count, plural, one {# download} other {# downloads}}"
    versionLabel: "v{version}"
    updatedLabel: "Aggiornato {date}"
    emptyTitle: "Nessun toolkit pubblicato"
    emptyDescription: "Sii il primo a creare un toolkit per questo gioco"
    emptyCta: "Crea toolkit"
  agents:
    title: "Agenti pubblici"
    emptyTitle: "Nessun agente disponibile"
    emptyDescription: "Configura un agente AI per assistere i giocatori"
    emptyCta: "Crea agente"
    ratingLabel: "{rating, number, ::.0} ({count, plural, one {# voto} other {# voti}})"
  knowledge:
    title: "Knowledge base"
    emptyTitle: "Nessuna documentazione"
    emptyDescription: "Carica regolamenti, FAQ o errata per arricchire la KB"
    emptyCta: "Carica documento"
    typeLabels:
      rulebook: "Regolamento"
      errata: "Errata"
      faq: "FAQ"
      "user-doc": "Documento utente"
  community:
    title: "Community"
    contributorsLabel: "{count, plural, one {# contributore} other {# contributori}}"
  error:
    title: "Errore di caricamento"
    description: "Non riusciamo a caricare il gioco. Riprova tra qualche istante."
    retryButton: "Riprova"
  notFound:
    title: "Gioco non trovato"
    description: "Il gioco che cerchi potrebbe essere stato rimosso o non è ancora pubblicato."
    backButton: "Torna al catalogo"
  cta:
    installLabel: "Accedi per installare"
    installDescription: "Crea un account gratuito per installare toolkit e agenti"
    loginLink: "Accedi"
    registerLink: "Registrati"
```

(Equivalent EN strings in `en.json`.)

## 6. Testing strategy (3-layer)

### 6.1 Visual contract (Playwright vs mockup baseline)

`apps/web/e2e/visual-migrated/sp3-shared-game-detail.spec.ts` — 1 desktop (1440×900) + 1 mobile (375×812). Compare contro `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-shared-game-detail-mockup-baseline-{desktop,mobile}-linux.png` (Phase 0 PR #575). Tolerance: 0.1% pixel diff (default Playwright).

**Hybrid masking**: zone con `data-dynamic` attr mascherate via `mask: [page.locator('[data-dynamic]')]` per evitare flake (rating dinamico, last-updated relative time, contributor count se livestream).

### 6.2 State coverage (Playwright × 5 stati × 2 viewport)

`apps/web/e2e/v2-states/shared-game-detail.spec.ts` — 10 PNG canonical Linux x86-64 in snapshot dir. Bootstrap mode:

```bash
gh workflow run 266963272 \
  --ref feature/issue-603-shared-game-detail-fe-v2 \
  -f mode=bootstrap \
  -f project_filter=both
```

Download artifact `visual-migrated-baselines-N` post-run + commit binari.

### 6.3 Behavioral unit tests (Vitest)

| Test file | Coverage |
|-----------|----------|
| `apps/web/src/components/ui/v2/shared-game-detail/__tests__/tabs.test.tsx` | WAI-ARIA roles, roving tabindex, ArrowLeft/Right wrap, Home/End, focus management |
| `apps/web/src/components/ui/v2/shared-game-detail/__tests__/sticky-cta.test.tsx` | Responsive position (sticky-bottom mobile / floating desktop), focus trap se modal |
| `apps/web/src/components/ui/v2/shared-game-detail/__tests__/contributors-strip.test.tsx` | Render 8 avatars, entity tinta applicata, link ai profili |
| `apps/web/src/hooks/__tests__/useSharedGameDetail.test.ts` | TanStack Query integration, SSR seed via `initialData`, refetch on retry |
| `apps/web/src/app/(public)/shared-games/[id]/__tests__/page-client.test.tsx` | 5-state FSM dispatch, override `?state=` clears data, hash sync via `useUrlHashState` |

### 6.4 Backend tests

| Test file | Coverage |
|-----------|----------|
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandlerTests.cs` (existing — extend) | Nested DTOs projection, IsTopRated/IsNew flag computation, threshold from config, soft-delete filter |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandlerIntegrationTests.cs` (new — Testcontainers) | Cross-BC join Game→Toolkit/AgentDefinition/VectorDocument, status filter, ordering |
| `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/EventHandlers/ToolkitChangedForCatalogAggregatesHandlerTests.cs` (existing — extend) | Tag eviction `shared-game:{id}` con real-cache-from-DI pattern |
| Idem per `AgentDefinitionChangedFor*` e `VectorDocumentIndexedFor*` | Idem |

**Real-cache-from-DI pattern** (mirror A.3a): `services.AddHybridCache().BuildServiceProvider()` → pre-seed entry tagged `shared-game:{id}` → run handler → re-call `GetOrCreateAsync` → assert factory re-invokes (= eviction worked). NON `Mock<IHybridCacheService>` (tag-store mismatch silent no-op).

### 6.5 A11y axe-core (CI gate)

PR #602 introduce job `frontend-a11y` con grep filter. Aggiungere a A.4 PR:
- Estendere `e2e/accessibility.spec.ts` con scenario `Shared Game Detail Page` che naviga a `/shared-games/{seed-id}` e asserisce 0 violations.
- Estendere grep filter in `.github/workflows/ci.yml` per includere il nuovo scenario.

## 7. Performance gates

| Metric | Target | Misurazione |
|--------|--------|-------------|
| `GET /shared-games/{id}` p95 cold-cache | <250ms | Backend integration test con `Stopwatch` su first call (HybridCache miss) |
| `GET /shared-games/{id}` p95 warm-cache | <50ms | Backend integration test su second call (L1 hit) |
| Frontend bundle delta | < +30KB after gzip | `apps/web/src/__tests__/bundle-size.test.ts` baseline bumped + tolerance 10240 bytes |
| LCP `/shared-games/{seed-id}` | <2.5s p75 mobile 4G | Lighthouse CI (informational, non-blocking Wave A) |

## 8. Commit boundary (monolithic single PR)

```
1. feat(shared-game-detail): backend SharedGameDetailDto extension + 3 nested public DTOs
   - +7 fields + 3 sealed records (PublishedToolkitPreviewDto/PublishedAgentPreviewDto/PublishedKbPreviewDto)
   - GetSharedGameByIdQueryHandler cross-BC LINQ projection
   - Cache invalidation: 3 event handler estesi con tag `shared-game:{id}`
   - Tests: unit (existing extend) + integration Testcontainers (new) + 3 event handler real-cache-from-DI

2. feat(shared-game-detail): v2 component family (8 components + index barrel)
   - Hero, Tabs (WAI-ARIA tablist), ToolkitListItem, AgentListItem, KbDocItem, ContributorsStrip, StickyCTA, EmptyState
   - Reuse: ContributorsSection 1:1 prod, ConnectionBar 1:1 prod, MeepleCardGame da A.3b, entityHsl helper
   - Vitest unit tests per ciascun componente con focus su a11y assertions

3. feat(shared-game-detail): page-client migration + 5-state surface + i18n
   - Server page.tsx rewrite (generateMetadata + 2-way Promise.allSettled SSR)
   - Client page-client.tsx 5-state FSM dispatcher
   - useSharedGameDetail hook (TanStack Query SSR seed + refetch)
   - lib/api/shared-games.ts extended con getSharedGameDetail + tipi
   - i18n IT+EN ~30 keys × 2 = 60 strings
   - Override ?state= guarded NODE_ENV !== 'production'

4. test(shared-game-detail): 3-layer testing infrastructure
   - Visual contract sp3-shared-game-detail.spec.ts (red phase, no baseline yet)
   - State coverage shared-game-detail.spec.ts × 5 stati × 2 viewport
   - Behavioral unit (Vitest) Tabs keyboard nav + StickyCTA + page-client FSM
   - axe-core scenario aggiunto a e2e/accessibility.spec.ts + grep filter ci.yml

5. chore(shared-games): deprecate SharedGameDetailModal.tsx
   - Mark @deprecated in JSDoc con redirect a /shared-games/[id] route
   - Audit call sites: grep -rn "SharedGameDetailModal" apps/web/src/
   - Remove from components/shared-games/index.ts barrel se 0 call sites
   - Comment di follow-up issue se ci sono call sites residui da migrare

6. chore(visual): bootstrap A.4 v2-states baselines (Linux x86-64)
   - gh workflow run 266963272 --ref feature/issue-603-shared-game-detail-fe-v2 -f mode=bootstrap
   - Download 12 PNG (2 visual-migrated + 10 v2-states)
   - Commit binari + push
```

## 9. Risk register

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Cross-BC join performance degrada p95 cold-cache | Medium | High | Indexed FK columns; batch query con `IN` clause; HybridCache copre warm path. Integration test misura cold p95. |
| `ContributorsSection` reuse 1:1 ha styling conflict con v2 | Medium | Medium | Wrap in v2 container con classi override; visual-test cattura regressioni; se rotto in 1:1 → fork copy in v2 dir |
| `ConnectionBar` PR #549/#552 path non ancora committed in `main-dev` | Low | Medium | Verifica esistenza pre-impl; se assente, depend chain bloccata fino a merge upstream |
| Cache invalidation tag-store mismatch (Pitfall #2620) | Low | High | Real-cache-from-DI test pattern obbligatorio per ogni event handler |
| #588 a11y AC non coperto da Tabs implementazione | Low | Medium | axe-core scenario + behavioral unit test su keyboard nav specifici |
| Bundle delta supera +30KB | Low | Low | Code-split per Tabs panel se necessario; lazy-load EmptyState/NotFound |

## 10. Definition of Done

- [ ] Backend: 7 fields + 3 nested DTOs + 3 invalidation handler estesi
- [ ] Backend: 23+ unit + 5+ integration tests pass
- [ ] Frontend: 8 v2 component + page-client + hook + API client + i18n IT/EN
- [ ] WAI-ARIA tablist completo (closes #588)
- [ ] 5-state surface override-clearable
- [ ] 12 PNG baseline canonical Linux x86-64 committed
- [ ] axe-core 0 violations (richiede #587 fix)
- [ ] `SharedGameDetailModal.tsx` `@deprecated` + audit call sites
- [ ] CI all green (Backend Unit, Frontend Build & Test, E2E Critical Paths, Migrated Routes Baseline, Frontend A11y axe-core, GitGuardian, codecov/patch)
- [ ] Code review APPROVED senza nit critical
- [ ] PR squash-merged a `main-dev`; branch eliminato
- [ ] Issue #603 chiusa via `Closes #603` body
- [ ] Issue #588 chiusa via cross-link comment "AC absorbed and verified by #603"
- [ ] MEMORY.md session entry aggiunta con esiti

---

**Spec authored**: 2026-04-28 — Wave A.4 kickoff
**Multi-expert review applied**: Wiegers (acceptance criteria SMART), Fowler (DTO interface segregation), Crispin (3-layer testing pyramid), Nygard (cache invalidation failure modes), Adzic (executable examples in §3.4)
