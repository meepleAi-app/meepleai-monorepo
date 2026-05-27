/**
 * Phase 2a (Issue #1605) — LibraryHub hybrid-hub orchestrator tests.
 *
 * Migrated from the Wave B.3 games-only suite (#574). The orchestrator now
 * consumes `useHybridHubItems` (the 3-source data layer) instead of
 * `useLibrary`, so the tests mock that hook wholesale and feed it
 * `HybridHubSources` of pre-mapped `HybridHubItem`s.
 *
 * Contract under test (plan §4c + #1605 AC):
 *   - 6 hub tabs: all / games / agents / kb / sessions / chat
 *   - 5-state FSM: default | loading | empty | filtered-empty | error,
 *     partial-failure-aware (error only when ALL ready sources fail).
 *   - `?state=...` URL override gated by NODE_ENV !== 'production' (test env).
 *   - Single click dispatcher: browse → router.push(item.href); select →
 *     toggle membership in `selected` Set.
 *   - Selection mode is game-scoped: enter button only in the games tab,
 *     forced to browse when leaving it.
 *   - Bulk delete: enter select mode (games tab) → toggle cards → confirm
 *     dialog → `Promise.allSettled` fan-out + clear selection + exit.
 *   - Hero stats are hybrid counts (games/agents/docs/chats) from totalCounts.
 *   - `useMiniNavConfig` invoked with breadcrumb 'Libreria · Hub' + Hub/Wishlist
 *     tabs + 'Aggiungi gioco' primary action.
 *   - clearFilters CTA from filtered-empty drops `?state=` override.
 *
 * Hooks mocked:
 *   - `next/navigation` (useRouter/useSearchParams/usePathname)
 *   - `@/hooks/queries/useHybridHubItems` (the data layer)
 *   - `@/hooks/queries/useLibrary` (useRemoveGameFromLibrary + useLibraryActivity)
 *   - `@/hooks/useMiniNavConfig` (verify call signature)
 *   - `@/hooks/useTranslation` is left real — it consumes IntlProvider seeded
 *     by the test wrapper, exercising the same react-intl path as production.
 *
 * `useLibraryView` is left real — it falls back to default 'grid' in jsdom
 * when localStorage is empty, exercising the same path as initial mount.
 */

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type {
  HybridHubSourceKey,
  UseHybridHubItemsResult,
} from '@/hooks/queries/useHybridHubItems';
import type { HybridHubSources } from '@/lib/library/hybrid-hub.derive';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

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

// ─── useHybridHubItems mock (the data layer) ──────────────────────────────

const hubMock = vi.fn<[], UseHybridHubItemsResult>();

vi.mock('@/hooks/queries/useHybridHubItems', () => ({
  useHybridHubItems: () => hubMock(),
  PER_SOURCE_CAP: 20,
}));

// ─── useRemoveGameFromLibrary + useLibraryActivity mocks ──────────────────

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

const useRemoveGameFromLibraryMock = vi.fn<[], MockMutationReturn>();
const useLibraryActivityMock = vi.fn<[], MockActivityReturn>(() => ({
  data: [],
  isLoading: false,
  isError: false,
  error: null,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  // useHybridHubItems is mocked wholesale, so its internal useLibrary never runs;
  // the orchestrator only pulls these two from this module directly.
  useLibrary: () => ({ data: undefined, isLoading: false, isError: false, error: null }),
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
  'pages.library.hero.title': 'La mia libreria',
  'pages.library.hero.subtitle': 'Tutta la tua collezione, gli agenti AI e le partite recenti.',
  'pages.library.hero.cta.add': 'Aggiungi gioco',
  'pages.library.hero.stats.totalGames': 'Giochi totali',
  'pages.library.hero.stats.kbReady': 'Knowledge base',
  'pages.library.hero.stats.wishlist': 'Wishlist',
  'pages.library.hero.stats.loaned': 'In prestito',
  'pages.library.hero.stats.agents': 'Agenti',
  'pages.library.hero.stats.docs': 'Documenti',
  'pages.library.hero.stats.chats': 'Chat',
  'pages.library.hubTabs.all': 'Tutti',
  'pages.library.hubTabs.games': 'Giochi',
  'pages.library.hubTabs.agents': 'Agenti',
  'pages.library.hubTabs.kb': 'KB',
  'pages.library.hubTabs.sessions': 'Sessioni',
  'pages.library.hubTabs.chat': 'Chat',
  'pages.library.filters.search.placeholder': 'Cerca per titolo, editore o anno…',
  'pages.library.filters.search.ariaLabel': 'Cerca nella libreria',
  'pages.library.filters.stato.label': 'Stato',
  'pages.library.filters.stato.owned': 'Posseduti',
  'pages.library.filters.stato.wishlist': 'Wishlist',
  'pages.library.filters.stato.loaned': 'In prestito',
  'pages.library.filters.stato.withKb': 'Con Knowledge Base',
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

// ─── hybrid hub fixture helpers ───────────────────────────────────────────

function gameItem(
  overrides: Partial<Extract<HybridHubItem, { entity: 'game' }>> = {}
): HybridHubItem {
  return {
    id: 'g1',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Kosmos',
    updatedAt: '2026-01-01T00:00:00Z',
    href: '/library/game-1',
    gameId: 'game-1',
    rating: 7,
    state: 'Owned',
    imageUrl: 'https://example.test/catan.jpg',
    hasKb: false,
    ...overrides,
  };
}

function sessionItem(
  overrides: Partial<Extract<HybridHubItem, { entity: 'session' }>> = {}
): HybridHubItem {
  return {
    id: 's1',
    entity: 'session',
    title: 'Session s1',
    subtitle: 'Alice',
    updatedAt: '2026-02-01T00:00:00Z',
    href: '/sessions/s1',
    status: 'Completed',
    playerCount: 4,
    ...overrides,
  };
}

function chatItem(
  overrides: Partial<Extract<HybridHubItem, { entity: 'chat' }>> = {}
): HybridHubItem {
  return {
    id: 'c1',
    entity: 'chat',
    title: 'How to play?',
    subtitle: 'Catan',
    updatedAt: '2026-03-01T00:00:00Z',
    href: '/chats/c1',
    messageCount: 3,
    ...overrides,
  };
}

const emptySources: HybridHubSources = { games: [], agents: [], kb: [], sessions: [], chat: [] };
const zeroCounts: Record<HybridHubSourceKey, number> = {
  games: 0,
  agents: 0,
  kb: 0,
  sessions: 0,
  chat: 0,
};
const noErrors: Record<HybridHubSourceKey, Error | null> = {
  games: null,
  agents: null,
  kb: null,
  sessions: null,
  chat: null,
};

/**
 * Default hub: 2 games, 1 session, 1 chat (agents/kb empty), no errors, loaded.
 * → totalCounts games:2 sessions:1 chat:1; FSM resolves to 'default'.
 */
function makeHub(overrides: Partial<UseHybridHubItemsResult> = {}): UseHybridHubItemsResult {
  const games = [
    gameItem(),
    gameItem({
      id: 'g2',
      title: 'Wingspan',
      href: '/library/game-2',
      gameId: 'game-2',
      state: 'Wishlist',
    }),
  ];
  const sessions = [sessionItem()];
  const chat = [chatItem()];
  const sources: HybridHubSources = { games, agents: [], kb: [], sessions, chat };
  return {
    sources,
    isLoading: false,
    allFailed: false,
    partialErrors: { ...noErrors },
    totalCounts: {
      games: games.length,
      agents: 0,
      kb: 0,
      sessions: sessions.length,
      chat: chat.length,
    },
    ...overrides,
  };
}

function renderHub(hub: UseHybridHubItemsResult) {
  hubMock.mockReturnValue(hub);
  return renderWithIntl(<LibraryHub />);
}

// Import after mocks declared so module resolution sees the mocked hooks.
import { LibraryHub } from '../LibraryHub';

describe('LibraryHub (Phase 2a hybrid hub)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    hubMock.mockReturnValue(makeHub());
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });
    useLibraryActivityMock.mockReturnValue({
      data: [],
      isLoading: false,
      isError: false,
      error: null,
    });
  });

  // ─── 6 hub tabs ──────────────────────────────────────────────────────────

  it('renders 6 hub tabs (all/games/agents/kb/sessions/chat)', () => {
    renderHub(makeHub());
    const tabs = screen.getAllByRole('tab');
    expect(tabs.map(tab => tab.getAttribute('data-tab-key'))).toEqual([
      'all',
      'games',
      'agents',
      'kb',
      'sessions',
      'chat',
    ]);
  });

  // ─── FSM: default ──────────────────────────────────────────────────────

  it('renders Hero + Tabs + Toolbar + HybridGrid + ActivityRail in default state', () => {
    const { container } = renderHub(makeHub());
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

  it('derives hero stats from hybrid totalCounts (games/agents/docs/chats)', () => {
    // makeHub default: games:2 agents:0 kb(docs):0 chats:1
    const { container } = renderHub(makeHub());
    const stats = container.querySelectorAll('[data-slot="library-hero-stat-value"]');
    expect(stats).toHaveLength(4);
    // Order = games, agents, docs, chats (plan §4c hero stat ordering)
    expect(stats[0]).toHaveTextContent('2');
    expect(stats[1]).toHaveTextContent('0');
    expect(stats[2]).toHaveTextContent('0');
    expect(stats[3]).toHaveTextContent('1');
  });

  // ─── FSM: loading ──────────────────────────────────────────────────────

  it('renders kind="loading" EmptyLibrary when hub.isLoading=true', () => {
    const { container } = renderHub(
      makeHub({ isLoading: true, sources: emptySources, totalCounts: { ...zeroCounts } })
    );
    const empty = container.querySelector('[data-slot="library-empty-state"]');
    expect(empty).not.toBeNull();
    expect(empty).toHaveAttribute('data-kind', 'loading');
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).not.toBeInTheDocument();
  });

  // ─── FSM: error (all sources fail) ───────────────────────────────────────

  it('all sources fail → error surface', () => {
    const { container } = renderHub(
      makeHub({ allFailed: true, sources: emptySources, totalCounts: { ...zeroCounts } })
    );
    const root = container.querySelector('[data-slot="library-hub-v2"]');
    expect(root).toHaveAttribute('data-state', 'error');
    const empty = container.querySelector('[data-slot="library-empty-state"]');
    expect(empty).toHaveAttribute('data-kind', 'error');
    expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
  });

  // ─── FSM: partial failure (1 source errors, others render) ───────────────

  it('partial failure: a source errors but others render, no error surface', () => {
    const { container } = renderHub(
      makeHub({
        partialErrors: { ...noErrors, sessions: new Error('x') },
        allFailed: false,
      })
    );
    const root = container.querySelector('[data-slot="library-hub-v2"]');
    expect(root).toHaveAttribute('data-state', 'default');
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-empty-state"]')).not.toBeInTheDocument();
  });

  // ─── FSM: empty ────────────────────────────────────────────────────────

  it('renders kind="empty" EmptyLibrary when all sources are empty', () => {
    const { container } = renderHub(
      makeHub({ sources: emptySources, totalCounts: { ...zeroCounts } })
    );
    const empty = container.querySelector('[data-slot="library-empty-state"]') as HTMLElement;
    expect(empty).toHaveAttribute('data-kind', 'empty');
    // Scope CTA query to empty state — Hero also renders an "Aggiungi gioco" CTA.
    expect(within(empty).getByRole('button', { name: 'Aggiungi gioco' })).toBeInTheDocument();
  });

  // ─── FSM: filtered-empty ───────────────────────────────────────────────

  it('renders kind="filtered-empty" EmptyLibrary when search query matches no items', () => {
    const { container } = renderHub(makeHub());
    const search = container.querySelector(
      '[data-slot="library-search-input"]'
    ) as HTMLInputElement;
    fireEvent.change(search, { target: { value: 'totally-nonexistent-item-title-xyz' } });
    const empty = container.querySelector('[data-slot="library-empty-state"]') as HTMLElement;
    expect(empty).toHaveAttribute('data-kind', 'filtered-empty');
    expect(within(empty).getByRole('button', { name: 'Cancella filtri' })).toBeInTheDocument();
  });

  // ─── State override (NODE_ENV !== 'production') ───────────────────────

  it('?state=loading override forces kind="loading" surface (NODE_ENV=test)', () => {
    searchParamsState.value = 'loading';
    const { container } = renderHub(makeHub());
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'loading'
    );
  });

  it('?state=empty override forces kind="empty" surface', () => {
    searchParamsState.value = 'empty';
    const { container } = renderHub(makeHub());
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'empty'
    );
  });

  it('?state=filtered-empty override forces kind="filtered-empty" surface', () => {
    searchParamsState.value = 'filtered-empty';
    const { container } = renderHub(makeHub());
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'filtered-empty'
    );
  });

  it('?state=error override forces kind="error" surface', () => {
    searchParamsState.value = 'error';
    const { container } = renderHub(makeHub());
    expect(container.querySelector('[data-slot="library-empty-state"]')).toHaveAttribute(
      'data-kind',
      'error'
    );
  });

  it('ignores unknown ?state= values and falls back to real FSM', () => {
    searchParamsState.value = 'totally-bogus';
    const { container } = renderHub(makeHub());
    expect(container.querySelector('[data-slot="library-hybrid-grid"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="library-empty-state"]')).not.toBeInTheDocument();
  });

  // ─── Tab switch filters the merged grid by entity ──────────────────────

  it('switching to "sessions" tab filters grid to session items only', () => {
    const { container } = renderHub(makeHub());
    const sessionsTab = container.querySelector('[data-tab-key="sessions"]') as HTMLButtonElement;
    expect(sessionsTab).not.toBeNull();
    fireEvent.click(sessionsTab);
    const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
    // default hub has 1 session
    expect(cards).toHaveLength(1);
    expect(cards[0].getAttribute('data-entry-id')).toBe('s1');
  });

  // ─── Click dispatcher: browse → router.push(item.href) ─────────────────

  it('clicking a card in browse mode navigates to item.href via router.push', () => {
    const { container } = renderHub(makeHub());
    const firstCard = container.querySelector(
      '[data-slot="library-grid-card"]'
    ) as HTMLButtonElement;
    expect(firstCard).not.toBeNull();
    fireEvent.click(firstCard);
    // default sort is 'recent' (updatedAt desc) → chat c1 (2026-03) is first.
    expect(routerPush).toHaveBeenCalledWith('/chats/c1');
  });

  // ─── Click dispatcher: select → toggles Set membership (games tab) ─────

  it('in select mode (games tab), clicking a card toggles selection', () => {
    const { container } = renderHub(makeHub());
    // Selection is game-scoped — switch to the games tab first.
    fireEvent.click(container.querySelector('[data-tab-key="games"]') as HTMLButtonElement);
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

  // ─── Select mode is game-scoped ──────────────────────────────────────────

  it('select-mode enter button is absent outside the games tab', () => {
    const { container } = renderHub(makeHub());
    // default tab is 'all' → no enter-select-mode button
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).toBeNull();
    // switch to games → button appears
    fireEvent.click(container.querySelector('[data-tab-key="games"]') as HTMLButtonElement);
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).not.toBeNull();
  });

  it('select mode is forced to browse when switching away from games tab', async () => {
    const user = userEvent.setup();
    const { container } = renderHub(makeHub());
    await user.click(screen.getByRole('tab', { name: /giochi/i }));
    await user.click(
      container.querySelector('[data-slot="library-enter-select-mode"]') as HTMLButtonElement
    );
    // BulkSelectionBar mounted in games select mode
    expect(container.querySelector('[data-slot="library-bulk-selection-bar"]')).not.toBeNull();
    // Switch to sessions tab → useEffect forces browse → bar unmounts.
    await user.click(screen.getByRole('tab', { name: /sessioni/i }));
    await waitFor(() => {
      expect(
        container.querySelector('[data-slot="library-bulk-selection-bar"]')
      ).not.toBeInTheDocument();
    });
  });

  // ─── Bulk delete fan-out ────────────────────────────────────────────────

  it('bulk delete fans out N parallel removeMutation calls and exits select mode', async () => {
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    useRemoveGameFromLibraryMock.mockReturnValue({ mutateAsync, isPending: false });

    const { container } = renderHub(makeHub());
    // Selection is game-scoped — switch to the games tab first.
    fireEvent.click(container.querySelector('[data-tab-key="games"]') as HTMLButtonElement);
    fireEvent.click(
      container.querySelector('[data-slot="library-enter-select-mode"]') as HTMLButtonElement
    );

    // Select both game cards (default hub: g1 + g2).
    const cards = container.querySelectorAll('[data-slot="library-grid-card"]');
    expect(cards).toHaveLength(2);
    fireEvent.click(cards[0] as HTMLButtonElement);
    fireEvent.click(cards[1] as HTMLButtonElement);
    const selectedIds = [
      cards[0].getAttribute('data-entry-id'),
      cards[1].getAttribute('data-entry-id'),
    ];

    // Open AlertDialog via Elimina trigger.
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
    const { container } = renderHub(makeHub());
    const hero = container.querySelector('[data-slot="library-hero-desktop"]') as HTMLElement;
    const cta = within(hero).getByRole('button', { name: 'Aggiungi gioco' });
    fireEvent.click(cta);
    expect(routerPush).toHaveBeenCalledWith('/library?action=add');
  });

  // ─── clearFilters drops ?state= override ───────────────────────────────

  it('clearFilters CTA from filtered-empty drops ?state= via router.push(pathname)', () => {
    searchParamsState.value = 'filtered-empty';
    renderHub(makeHub());
    const cta = screen.getByRole('button', { name: 'Cancella filtri' });
    fireEvent.click(cta);
    // Orchestrator should call router.push(pathname) to drop the ?state= override.
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  // ─── useMiniNavConfig invocation contract ──────────────────────────────

  it('registers mini-nav config with breadcrumb + Hub/Wishlist tabs + primaryAction', () => {
    renderHub(makeHub());
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
      count: 0, // Phase 2a: wishlist count not yet wired into the hybrid hub
    });
    expect(lastCall.primaryAction.label).toBe('Aggiungi gioco');
    expect(typeof lastCall.primaryAction.onClick).toBe('function');
  });
});
