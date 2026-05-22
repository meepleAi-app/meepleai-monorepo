/**
 * Wave B.3 (Issue #574) — LibraryHub orchestrator tests.
 *
 * Mirrors Wave B.1 GamesLibraryView and B.2 AgentsLibraryView tests:
 *   - stub `useLibrary` + `useRemoveGameFromLibrary` + `next/navigation` search
 *     params + i18n via IntlProvider seeded with the actual `pages.library.*`
 *     keys from `it.json`. The orchestrator is the only stateful piece — the 6
 *     feature components (LibraryHeroDesktop, LibraryTabs, LibraryHybridGrid,
 *     EmptyLibrary, BulkSelectionBar, RecentActivityRail) are pure label-driven
 *     (covered separately under `components/features/library/__tests__/`).
 *
 * Contract under test (spec §3.2 + §4.2):
 *   - 5-state FSM: default | loading | empty | filtered-empty | error
 *   - `?state=...` URL override gated by NODE_ENV !== 'production' (test env)
 *   - Single click dispatcher: browse → router.push(/games/:id); select →
 *     toggle membership in `selected` Set
 *   - Tab switch (`all/kb/loaned`) filters via `filterByEntity`
 *   - Bulk delete: enter select mode → toggle cards → confirm dialog →
 *     `Promise.allSettled` fan-out + clear selection + exit select mode
 *   - Hero stats derived from entries (totalGames, kbReady, wishlist, loaned)
 *   - `useMiniNavConfig` invoked with breadcrumb 'Libreria · Hub' + Hub/Wishlist
 *     tabs + 'Aggiungi gioco' primary action
 *   - clearFilters CTA from filtered-empty drops `?state=` override
 *
 * Hooks mocked:
 *   - `next/navigation` (useRouter/useSearchParams/usePathname)
 *   - `@/hooks/queries/useLibrary` (useLibrary + useRemoveGameFromLibrary)
 *   - `@/hooks/useMiniNavConfig` (verify call signature)
 *   - `@/hooks/useTranslation` is left real — it consumes IntlProvider seeded
 *     by the test wrapper, exercising the same react-intl path as production.
 *
 * `useLibraryView` is left real — it falls back to default 'grid' in jsdom
 * when localStorage is empty, exercising the same path as initial mount.
 */

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type {
  GameStateType,
  PaginatedLibraryResponse,
  UserLibraryEntry,
} from '@/lib/api/schemas/library.schemas';

// ─── next/navigation mocks ────────────────────────────────────────────────

const searchParamsState = { value: '' };
const routerPush = vi.fn();
const routerReplace = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => {
      if (key === 'state') return searchParamsState.value || null;
      return null;
    },
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
  usePathname: () => '/library',
}));

// ─── useLibrary + useRemoveGameFromLibrary + useLibraryActivity mocks ─────

type MockLibraryReturn = {
  data?: PaginatedLibraryResponse;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
  refetch?: () => void;
};

type MockMutationReturn = {
  mutateAsync: (gameId: string) => Promise<void>;
  isPending: boolean;
};

type MockActivityReturn = {
  data: ReadonlyArray<unknown> | undefined;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
};

const useLibraryMock = vi.fn<[], MockLibraryReturn>();
const useRemoveGameFromLibraryMock = vi.fn<[], MockMutationReturn>();
const useLibraryActivityMock = vi.fn<[], MockActivityReturn>(() => ({
  data: [],
  isLoading: false,
  isError: false,
  error: null,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => useLibraryMock(),
  useRemoveGameFromLibrary: () => useRemoveGameFromLibraryMock(),
  useLibraryActivity: () => useLibraryActivityMock(),
}));

// ─── useMiniNavConfig mock (verify invocation) ────────────────────────────

const useMiniNavConfigMock = vi.fn();

vi.mock('@/hooks/useMiniNavConfig', () => ({
  useMiniNavConfig: (cfg: unknown) => useMiniNavConfigMock(cfg),
}));

// ─── react-intl messages (subset matching it.json `pages.library.*`) ──────

const MESSAGES: Record<string, string> = {
  'pages.library.hero.title': 'La tua libreria',
  'pages.library.hero.subtitle': 'Tutti i giochi che possiedi, in attesa di giocare o in prestito.',
  'pages.library.hero.cta.add': 'Aggiungi gioco',
  'pages.library.hero.stats.totalGames': 'Giochi totali',
  'pages.library.hero.stats.kbReady': 'KB pronte',
  'pages.library.hero.stats.wishlist': 'Wishlist',
  'pages.library.hero.stats.loaned': 'In prestito',
  'pages.library.tabs.all': 'Tutti',
  'pages.library.tabs.kb': 'KB pronte',
  'pages.library.tabs.loaned': 'In prestito',
  'pages.library.filters.search.placeholder': 'Cerca per titolo, editore o anno…',
  'pages.library.filters.search.ariaLabel': 'Cerca nella libreria',
  'pages.library.sort.label': 'Ordina',
  'pages.library.sort.ariaLabel': 'Ordina i risultati',
  'pages.library.sort.recent': 'Più recenti',
  'pages.library.sort.title': 'Titolo A-Z',
  'pages.library.sort.rating': 'Voto più alto',
  'pages.library.sort.state': 'Per stato',
  'pages.library.view.ariaLabel': 'Modalità di visualizzazione',
  'pages.library.view.grid': 'Griglia',
  'pages.library.view.list': 'Lista',
  'pages.library.view.compact': 'Compatta',
  'pages.library.selectionMode.enter': 'Seleziona',
  'pages.library.selectionMode.enterAriaLabel': 'Entra in modalità selezione',
  'pages.library.selectionMode.exit': 'Annulla',
  'pages.library.selectionMode.exitAriaLabel': 'Esci dalla modalità selezione',
  'pages.library.selectionMode.selectedCount':
    '{count, plural, =0 {Nessuno selezionato} =1 {1 selezionato} other {# selezionati}}',
  'pages.library.bulk.actions.delete': 'Elimina',
  'pages.library.bulk.confirm.deleteTitle':
    '{count, plural, =1 {Confermi rimozione di 1 gioco?} other {Confermi rimozione di # giochi?}}',
  'pages.library.bulk.confirm.deleteMessage':
    'I giochi selezionati saranno rimossi dalla libreria. La PDF KB resterà disponibile.',
  'pages.library.bulk.confirm.confirmCta': 'Conferma',
  'pages.library.bulk.confirm.cancelCta': 'Annulla',
  'pages.library.emptyState.default.title': 'La tua libreria è vuota',
  'pages.library.emptyState.default.subtitle': 'Aggiungi il tuo primo gioco per iniziare.',
  'pages.library.emptyState.default.cta': 'Aggiungi gioco',
  'pages.library.emptyState.filteredEmpty.title': 'Nessun risultato',
  'pages.library.emptyState.filteredEmpty.subtitle': 'Prova a modificare la ricerca o i filtri.',
  'pages.library.emptyState.filteredEmpty.cta': 'Cancella filtri',
  'pages.library.emptyState.error.title': 'Caricamento fallito',
  'pages.library.emptyState.error.subtitle':
    'Non siamo riusciti a recuperare la tua libreria. Riprova.',
  'pages.library.emptyState.error.cta': 'Riprova',
};

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

// ─── fixture helpers ──────────────────────────────────────────────────────

const NOW = '2026-04-30T10:00:00.000Z';
const USER_ID = '00000000-0000-4000-8000-aaaaaaaaaaaa';

function makeEntry(
  overrides: Partial<UserLibraryEntry> &
    Pick<UserLibraryEntry, 'id' | 'gameId' | 'gameTitle' | 'currentState'>
): UserLibraryEntry {
  return {
    userId: USER_ID,
    gamePublisher: null,
    gameYearPublished: null,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: NOW,
    notes: null,
    isFavorite: false,
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: false,
    agentIsOwned: false,
    minPlayers: null,
    maxPlayers: null,
    playingTimeMinutes: null,
    complexityRating: null,
    averageRating: null,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  } as UserLibraryEntry;
}

// 6 entries: 4 Owned (2 hasKb), 1 InPrestito, 1 Wishlist
//   → totalGames=6, kbReady=2, wishlist=1, loaned=1
//   → tab counts: all=6, kb=2, loaned=1
const ENTRIES_6: UserLibraryEntry[] = [
  makeEntry({
    id: 'entry-1',
    gameId: 'game-1',
    gameTitle: 'Catan',
    gamePublisher: 'KOSMOS',
    currentState: 'Owned' as GameStateType,
    hasKb: true,
    kbCardCount: 12,
    kbIndexedCount: 12,
    averageRating: 7.2,
  }),
  makeEntry({
    id: 'entry-2',
    gameId: 'game-2',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier',
    currentState: 'Owned' as GameStateType,
    hasKb: true,
    kbCardCount: 8,
    kbIndexedCount: 8,
    averageRating: 8.1,
  }),
  makeEntry({
    id: 'entry-3',
    gameId: 'game-3',
    gameTitle: 'Terraforming Mars',
    gamePublisher: 'FryxGames',
    currentState: 'Owned' as GameStateType,
    averageRating: 8.4,
  }),
  makeEntry({
    id: 'entry-4',
    gameId: 'game-4',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    currentState: 'Owned' as GameStateType,
    averageRating: 7.8,
  }),
  makeEntry({
    id: 'entry-5',
    gameId: 'game-5',
    gameTitle: 'Carcassonne',
    gamePublisher: 'Z-Man Games',
    currentState: 'InPrestito' as GameStateType,
    averageRating: 7.4,
  }),
  makeEntry({
    id: 'entry-6',
    gameId: 'game-6',
    gameTitle: 'Pandemic Legacy',
    gamePublisher: 'Z-Man Games',
    currentState: 'Wishlist' as GameStateType,
    averageRating: 8.6,
  }),
];

function paginated(items: UserLibraryEntry[]): PaginatedLibraryResponse {
  return {
    items,
    page: 1,
    pageSize: 50,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}

// Import after mocks declared so module resolution sees the mocked hooks.
import { LibraryHub } from '../LibraryHub';

describe('LibraryHub (Wave B.3)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    useLibraryMock.mockReturnValue({
      data: paginated(ENTRIES_6),
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });
  });

  // ─── FSM: default ──────────────────────────────────────────────────────

  it('renders Hero + Tabs + Toolbar + HybridGrid + ActivityRail in default state', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const root = container.querySelector('[data-slot="library-hub-v2"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-state', 'default');
    expect(container.querySelector('[data-slot="library-hero-desktop"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-tabs"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-toolbar"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-activity-rail"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-empty-state"]')).not.toBeInTheDocument();
  });

  it('derives hero stats from entries via deriveHeroStats (6 totalGames, 2 kbReady, 1 wishlist, 1 loaned)', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const stats = container.querySelectorAll('[data-slot="library-hero-stat-value"]');
    expect(stats).toHaveLength(4);
    // Order = totalGames, kbReady, wishlist, loaned (per orchestrator §3.2 stat ordering)
    expect(stats[0]).toHaveTextContent('6');
    expect(stats[1]).toHaveTextContent('2');
    expect(stats[2]).toHaveTextContent('1');
    expect(stats[3]).toHaveTextContent('1');
  });

  // ─── FSM: loading ──────────────────────────────────────────────────────

  it('renders kind="loading" EmptyLibrary when useLibrary.isLoading=true', () => {
    useLibraryMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const empty = container.querySelector('[data-slot="library-empty-state"]');
    expect(empty).not.toBeNull();
    expect(empty).toHaveAttribute('data-kind', 'loading');
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).not.toBeInTheDocument();
  });

  // ─── FSM: error ────────────────────────────────────────────────────────

  it('renders kind="error" EmptyLibrary when useLibrary.isError=true', () => {
    useLibraryMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('boom'),
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const empty = container.querySelector('[data-slot="library-empty-state"]');
    expect(empty).toHaveAttribute('data-kind', 'error');
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  // ─── FSM: empty ────────────────────────────────────────────────────────

  it('renders kind="empty" EmptyLibrary when data.items is empty array', () => {
    useLibraryMock.mockReturnValue({
      data: paginated([]),
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const empty = container.querySelector('[data-slot="library-empty-state"]') as HTMLElement;
    expect(empty).toHaveAttribute('data-kind', 'empty');
    // Scope CTA query to empty state — Hero also renders an "Aggiungi gioco" CTA.
    expect(within(empty).getByRole('button', { name: 'Aggiungi gioco' })).toBeInTheDocument();
  });

  // ─── FSM: filtered-empty ───────────────────────────────────────────────

  it('renders kind="filtered-empty" EmptyLibrary when search query matches no entries', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const search = container.querySelector(
      '[data-slot="library-search-input"]'
    ) as HTMLInputElement;
    fireEvent.change(search, { target: { value: 'totally-nonexistent-game-title-xyz' } });
    const empty = container.querySelector('[data-slot="library-empty-state"]') as HTMLElement;
    expect(empty).toHaveAttribute('data-kind', 'filtered-empty');
    expect(within(empty).getByRole('button', { name: 'Cancella filtri' })).toBeInTheDocument();
  });

  // ─── State override (NODE_ENV !== 'production') ───────────────────────

  it('?state=loading override forces kind="loading" surface (NODE_ENV=test)', () => {
    searchParamsState.value = 'loading';
    const { container } = renderWithIntl(<LibraryHub />);
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'loading'
    );
  });

  it('?state=empty override forces kind="empty" surface', () => {
    searchParamsState.value = 'empty';
    const { container } = renderWithIntl(<LibraryHub />);
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'empty'
    );
  });

  it('?state=filtered-empty override forces kind="filtered-empty" surface', () => {
    searchParamsState.value = 'filtered-empty';
    const { container } = renderWithIntl(<LibraryHub />);
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'filtered-empty'
    );
  });

  it('?state=error override forces kind="error" surface', () => {
    searchParamsState.value = 'error';
    const { container } = renderWithIntl(<LibraryHub />);
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'error'
    );
  });

  it('ignores unknown ?state= values and falls back to real FSM', () => {
    searchParamsState.value = 'totally-bogus';
    const { container } = renderWithIntl(<LibraryHub />);
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-empty-state"]')).not.toBeInTheDocument();
  });

  // ─── Tab switch (LibraryEntityKey filtering) ───────────────────────────

  it('switching to "kb" tab filters grid to entries with hasKb=true (2 cards)', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const kbTab = container.querySelector('[data-tab-key="kb"]') as HTMLButtonElement;
    expect(kbTab).not.toBeNull();
    fireEvent.click(kbTab);
    const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
    expect(cards).toHaveLength(2);
    // Entries with hasKb=true are entry-1 (Catan) + entry-2 (Wingspan)
    const ids = Array.from(cards).map(c => c.getAttribute('data-entry-id'));
    expect(ids).toEqual(expect.arrayContaining(['entry-1', 'entry-2']));
  });

  // ─── Click dispatcher: browse → router.push ────────────────────────────

  it('clicking a card in browse mode navigates to /library/{gameId} via router.push', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const firstCard = container.querySelector(
      '[data-slot="library-grid-card"]'
    ) as HTMLButtonElement;
    expect(firstCard).not.toBeNull();
    const entryId = firstCard.getAttribute('data-entry-id');
    // LibraryHub maps entryId → entry.gameId for navigation (#871 IA refactor).
    // Fixture: entry-N → game-N (see ENTRIES above).
    const gameId = entryId?.replace('entry-', 'game-');
    fireEvent.click(firstCard);
    expect(routerPush).toHaveBeenCalledWith(`/library/${gameId}`);
  });

  // ─── Click dispatcher: select → toggles Set membership ─────────────────

  it('in select mode, clicking a card toggles selection (aria-pressed reflects state)', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    // Enter select mode
    const enterBtn = container.querySelector(
      '[data-slot="library-enter-select-mode"]'
    ) as HTMLButtonElement;
    fireEvent.click(enterBtn);

    const firstCard = container.querySelector(
      '[data-slot="library-grid-card"]'
    ) as HTMLButtonElement;
    expect(firstCard).toHaveAttribute('aria-pressed', 'false');
    fireEvent.click(firstCard);
    expect(firstCard).toHaveAttribute('aria-pressed', 'true');
    // Browse-mode router.push must NOT fire in select mode.
    expect(routerPush).not.toHaveBeenCalled();
    // Toggle off
    fireEvent.click(firstCard);
    expect(firstCard).toHaveAttribute('aria-pressed', 'false');
  });

  // ─── Bulk delete fan-out ────────────────────────────────────────────────

  it('bulk delete fans out N parallel removeMutation calls and exits select mode', async () => {
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    useRemoveGameFromLibraryMock.mockReturnValue({ mutateAsync, isPending: false });

    const { container } = renderWithIntl(<LibraryHub />);
    // Enter select mode
    fireEvent.click(
      container.querySelector('[data-slot="library-enter-select-mode"]') as HTMLButtonElement
    );

    // Select 2 cards
    const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
    fireEvent.click(cards[0] as HTMLButtonElement);
    fireEvent.click(cards[1] as HTMLButtonElement);
    const selectedIds = [
      cards[0].getAttribute('data-entry-id'),
      cards[1].getAttribute('data-entry-id'),
    ];

    // Open AlertDialog via Elimina trigger
    fireEvent.click(
      container.querySelector('[data-slot="library-bulk-selection-archive"]') as HTMLButtonElement
    );

    // Radix portals dialog content into document.body — query via screen.
    const confirmBtn = await waitFor(() => screen.getByRole('button', { name: 'Conferma' }));
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledTimes(2);
    });
    // Both selected IDs were dispatched (order not guaranteed under fan-out).
    const calledWith = mutateAsync.mock.calls.map(c => c[0]);
    expect(calledWith).toEqual(expect.arrayContaining(selectedIds));

    // After settle: bar should unmount (selectionMode → 'browse').
    await waitFor(() => {
      expect(
        container.querySelector('[data-slot="library-bulk-selection-bar"]')
      ).not.toBeInTheDocument();
    });
  });

  // ─── Hero CTA → router.push add-game query ─────────────────────────────

  it('clicking hero "Aggiungi gioco" CTA navigates to /library?action=add', () => {
    const { container } = renderWithIntl(<LibraryHub />);
    const hero = container.querySelector('[data-slot="library-hero-desktop"]') as HTMLElement;
    const cta = within(hero).getByRole('button', { name: 'Aggiungi gioco' });
    fireEvent.click(cta);
    expect(routerPush).toHaveBeenCalledWith('/library?action=add');
  });

  // ─── clearFilters drops ?state= override ───────────────────────────────

  it('clearFilters CTA from filtered-empty drops ?state= via router.push(pathname)', () => {
    searchParamsState.value = 'filtered-empty';
    renderWithIntl(<LibraryHub />);
    const cta = screen.getByRole('button', { name: 'Cancella filtri' });
    fireEvent.click(cta);
    // Orchestrator should call router.push(pathname) to drop the ?state= override.
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  // ─── useMiniNavConfig invocation contract ──────────────────────────────

  it('registers mini-nav config with breadcrumb + Hub/Wishlist tabs + primaryAction', () => {
    renderWithIntl(<LibraryHub />);
    expect(useMiniNavConfigMock).toHaveBeenCalled();
    const lastCall = useMiniNavConfigMock.mock.calls.at(-1)?.[0] as {
      breadcrumb: string;
      tabs: ReadonlyArray<{ id: string; label: string; href: string; count?: number }>;
      activeTabId: string;
      primaryAction: { label: string; icon: string; onClick: () => void };
    };
    expect(lastCall.breadcrumb).toBe('Libreria · Hub');
    expect(lastCall.activeTabId).toBe('hub');
    expect(lastCall.tabs).toHaveLength(2);
    expect(lastCall.tabs[0]).toMatchObject({ id: 'hub', href: '/library' });
    expect(lastCall.tabs[1]).toMatchObject({
      id: 'wishlist',
      href: '/library/wishlist',
      count: 1, // 1 Wishlist entry in fixture
    });
    expect(lastCall.primaryAction.label).toBe('Aggiungi gioco');
    expect(typeof lastCall.primaryAction.onClick).toBe('function');
  });
});
