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
 *   - `@/hooks/queries/useLibrary` (useRemoveGameFromLibrary)
 *   - `@/hooks/useActivityFeed` (cross-entity rail feed — Phase 3b #1593)
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
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

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

// ─── useRemoveGameFromLibrary mocks ──────────────────────────────────────

type MockMutationReturn = {
  mutateAsync: (gameId: string) => Promise<void>;
  isPending: boolean;
};

type MockLibraryReturn = {
  data: { items: UserLibraryEntry[] } | undefined;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
};

const useRemoveGameFromLibraryMock = vi.fn<[], MockMutationReturn>();
const libraryMock = vi.fn<[], MockLibraryReturn>(() => ({
  data: undefined,
  isLoading: false,
  isError: false,
  error: null,
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  // useHybridHubItems is mocked wholesale, so its internal useLibrary never runs;
  // the orchestrator only pulls these two from this module directly.
  useLibrary: () => libraryMock(),
  useRemoveGameFromLibrary: () => useRemoveGameFromLibraryMock(),
}));

// ─── useMiniNavConfig mock (verify invocation) ────────────────────────────

const useMiniNavConfigMock = vi.fn();

vi.mock('@/hooks/useMiniNavConfig', () => ({
  useMiniNavConfig: (cfg: unknown) => useMiniNavConfigMock(cfg),
}));

// ─── useActivityFeed mock (Phase 3b #1593) ────────────────────────────────

type MockActivityFeedReturn = {
  data:
    | {
        items: Array<{ id: string; kind: string; entityTitle: string; timestamp: string }>;
        count: number;
      }
    | undefined;
  isLoading: boolean;
  isSuccess: boolean;
  isError: boolean;
  error: Error | null;
};

const useActivityFeedMock = vi.fn<[], MockActivityFeedReturn>();

vi.mock('@/hooks/useActivityFeed', () => ({
  useActivityFeed: () => useActivityFeedMock(),
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
  // ─── RecentActivityRail keys (Phase 3b #1593) ───
  'pages.library.activityRail.title': 'Attività recente',
  'pages.library.activityRail.empty': 'Nessuna attività recente.',
  'pages.library.activityRail.error': "Impossibile caricare l'attività.",
  // ─── AdvancedFiltersDrawer header/footer keys (Phase 3a #1606) ───
  'pages.library.filters.title': 'Più filtri',
  'pages.library.filters.description': "Filtra la libreria per dimensioni specifiche dell'entità.",
  'pages.library.filters.clear': 'Reimposta',
  'pages.library.filters.apply': 'Applica',
  'common.cancel': 'Annulla',
  // ─── Drawer section labels — game scope ───
  'pages.library.filters.section.state': 'Stato',
  'pages.library.filters.section.withKb': 'Solo con Knowledge Base',
  'pages.library.filters.section.rating': 'Rating minimo',
  'pages.library.filters.section.players': 'Numero di giocatori',
  'pages.library.filters.section.year': 'Anno di pubblicazione',
  // ─── Drawer section labels — agent scope ───
  'pages.library.filters.section.agentType': 'Tipo di agente',
  'pages.library.filters.section.activeOnly': 'Solo attivi',
  // ─── Drawer section labels — session scope ───
  'pages.library.filters.section.sessionStatus': 'Stato sessione',
  'pages.library.filters.section.sessionType': 'Tipo sessione',
  'pages.library.filters.section.playerCount': 'Giocatori (min)',
  // ─── Drawer section labels — kb scope ───
  'pages.library.filters.section.processingState': 'Stato di elaborazione',
  'pages.library.filters.kbState.ready': 'Pronto',
  'pages.library.filters.kbState.pending': 'In elaborazione',
  'pages.library.filters.kbState.failed': 'Errore',
  // ─── Drawer section labels — chat scope ───
  'pages.library.filters.section.messageCountMin': 'Messaggi (min)',
  // ─── Drawer checkbox option labels ───
  'pages.library.filters.state.owned': 'Posseduto',
  'pages.library.filters.state.wishlist': 'Wishlist',
  'pages.library.filters.state.loaned': 'In prestito',
  // ─── gamesTab i18n keys (#1566) ───
  'pages.library.gamesTab.filters.search.placeholder': 'Cerca per titolo…',
  'pages.library.gamesTab.filters.search.ariaLabel': 'Cerca giochi nella tua libreria',
  'pages.library.gamesTab.filters.search.clearAriaLabel': 'Pulisci ricerca',
  'pages.library.gamesTab.filters.status.label': 'Stato',
  'pages.library.gamesTab.filters.status.options.all': 'Tutti',
  'pages.library.gamesTab.filters.status.options.owned': 'Posseduti',
  'pages.library.gamesTab.filters.status.options.wishlist': 'Wishlist',
  'pages.library.gamesTab.filters.status.options.played': 'Giocati',
  'pages.library.gamesTab.filters.sort.label': 'Ordina',
  'pages.library.gamesTab.filters.sort.options.last-played': 'Ultima partita',
  'pages.library.gamesTab.filters.sort.options.rating': 'Rating',
  'pages.library.gamesTab.filters.sort.options.title': 'Titolo A-Z',
  'pages.library.gamesTab.filters.sort.options.year': 'Anno',
  'pages.library.gamesTab.filters.view.label': 'Vista',
  'pages.library.gamesTab.filters.view.options.grid': 'Griglia',
  'pages.library.gamesTab.filters.view.options.list': 'Lista',
  'pages.library.gamesTab.filters.resultCount': '{count, plural, one {# gioco} other {# giochi}}',
  'pages.library.gamesTab.emptyState.empty.title': 'Aggiungi il tuo primo gioco',
  'pages.library.gamesTab.emptyState.empty.subtitle': 'Costruisci la tua libreria per iniziare.',
  'pages.library.gamesTab.emptyState.empty.cta': 'Aggiungi gioco',
  'pages.library.gamesTab.emptyState.filteredEmpty.title': 'Nessun risultato',
  'pages.library.gamesTab.emptyState.filteredEmpty.subtitle':
    'Prova ad allargare i filtri o azzerarli.',
  'pages.library.gamesTab.emptyState.filteredEmpty.cta': 'Azzera filtri',
  'pages.library.gamesTab.emptyState.error.title': 'Errore di caricamento',
  'pages.library.gamesTab.emptyState.error.subtitle': 'Impossibile recuperare la libreria.',
  'pages.library.gamesTab.emptyState.error.cta': 'Riprova',
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

// ─── installMatchMedia — force desktop (Radix Dialog) mode ──────────────────
// Without this, Vaul renders a bottom sheet in jsdom which exposes no
// role="dialog". installMatchMedia(true) makes window.matchMedia return
// matches=true so Radix Dialog (desktop path) is taken instead.
function installMatchMedia(matches: boolean) {
  const listeners = new Set<(e: MediaQueryListEvent) => void>();
  const mql = {
    matches,
    media: '(min-width: 768px)',
    addEventListener: (_e: string, cb: (e: MediaQueryListEvent) => void) => listeners.add(cb),
    removeEventListener: (_e: string, cb: (e: MediaQueryListEvent) => void) => listeners.delete(cb),
    onchange: null,
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue(mql),
  });
}

// Import after mocks declared so module resolution sees the mocked hooks.
import { LibraryHub } from '../LibraryHub';

describe('LibraryHub (Phase 2a hybrid hub)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    hubMock.mockReturnValue(makeHub());
    libraryMock.mockReset();
    libraryMock.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null });
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });
    useActivityFeedMock.mockReturnValue({
      data: { items: [], count: 0 },
      isLoading: false,
      isSuccess: true,
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
  // #1566: The games tab now renders GamesResultsGrid (no hybrid grid cards).
  // #1566: the enter-select-mode button was removed (not moved). There is no
  // rendered path that shows [data-slot="library-enter-select-mode"]. Confirm
  // it is absent on the 'all' tab.
  it('select-mode enter button is absent on the all tab', () => {
    const { container } = renderHub(makeHub());
    const enterBtn = container.querySelector(
      '[data-slot="library-enter-select-mode"]'
    ) as HTMLButtonElement;
    expect(enterBtn).toBeNull();
  });

  // ─── Select mode is game-scoped ──────────────────────────────────────────

  // #1566: The library-enter-select-mode button was deleted. It is absent from
  // the games branch (GamesFiltersInline replaces the toolbar) and also absent
  // from the else-branch toolbar (the button was not re-added there). Confirm all tabs.
  it('select-mode enter button is absent on all tabs (button deleted by #1566)', () => {
    const { container } = renderHub(makeHub());
    // default tab is 'all' → no enter-select-mode button
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).toBeNull();
    // switch to games → games branch renders GamesFiltersInline, still no button
    fireEvent.click(container.querySelector('[data-tab-key="games"]') as HTMLButtonElement);
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).toBeNull();
    // switch to sessions tab → else-branch toolbar, button was not re-added → still absent
    fireEvent.click(container.querySelector('[data-tab-key="sessions"]') as HTMLButtonElement);
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).toBeNull();
  });

  // #1566: The BulkSelectionBar is still rendered outside the tab branch when
  // selectionMode === 'select'. Since the enter-button is now dead code, we can
  // only programmatically verify the bar would still unmount on tab switch via
  // the useEffect. This test verifies the useEffect clears selection mode when
  // switching tabs even without entering from a button press.
  it('select mode is forced to browse when switching away from games tab', async () => {
    const user = userEvent.setup();
    const { container } = renderHub(makeHub());
    // Switch to sessions tab first and back — useEffect on tab change fires.
    await user.click(screen.getByRole('tab', { name: /sessioni/i }));
    // BulkSelectionBar never mounted (not in select mode) — should be absent.
    await waitFor(() => {
      expect(
        container.querySelector('[data-slot="library-bulk-selection-bar"]')
      ).not.toBeInTheDocument();
    });
    // The useEffect guard is still present and functional; the FSM for
    // selectionMode reset is tested via the bulk-delete test which enters select
    // mode programmatically via handleEnterSelectMode callback.
  });

  // ─── Bulk delete fan-out ────────────────────────────────────────────────
  // #1566: The games tab now renders the GamesResultsGrid branch. The BulkSelectionBar
  // component and handleBulkDelete callback remain in place for future re-wiring;
  // this test verifies BulkSelectionBar is absent on the games tab because the
  // enter-select-mode button was deleted (not moved) by #1566.
  it('bulk-select bar is absent on the games tab (enter-button path removed by #1566)', async () => {
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });

    const { container } = renderHub(makeHub());
    // Switch to games tab → games branch renders, no toolbar, no enter-select button.
    fireEvent.click(container.querySelector('[data-tab-key="games"]') as HTMLButtonElement);
    expect(container.querySelector('[data-slot="library-enter-select-mode"]')).toBeNull();
    expect(container.querySelector('[data-slot="library-bulk-selection-bar"]')).toBeNull();
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

// ─── Games tab (#1566) ────────────────────────────────────────────────────

function libEntry(
  id: string,
  title: string,
  extra: Partial<UserLibraryEntry> = {}
): UserLibraryEntry {
  return {
    id,
    userId: 'u1',
    gameId: `game-${id}`,
    gameTitle: title,
    gamePublisher: 'Pub',
    gameYearPublished: 2000,
    gameIconUrl: '',
    gameImageUrl: '',
    addedAt: '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    agentIsOwned: true,
    hasRagAccess: false,
    ownershipDeclaredAt: null,
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    complexityRating: null,
    averageRating: null,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
    timesPlayed: 0,
    lastPlayed: null,
    ...extra,
  } as UserLibraryEntry;
}

function seedGamesLibrary(entries: UserLibraryEntry[]): void {
  libraryMock.mockReturnValue({
    data: { items: entries },
    isLoading: false,
    isError: false,
    error: null,
  });
}

describe('LibraryHub — games tab (#1566)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    hubMock.mockReturnValue(makeHub());
    libraryMock.mockReset();
    libraryMock.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null });
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });
    useActivityFeedMock.mockReturnValue({
      data: { items: [], count: 0 },
      isLoading: false,
      isSuccess: true,
      isError: false,
      error: null,
    });
  });

  it('renders GamesFiltersInline + GamesResultsGrid when tab=games with entries', async () => {
    hubMock.mockReturnValue(
      makeHub({ totalCounts: { games: 1, agents: 0, kb: 0, sessions: 0, chat: 0 } })
    );
    seedGamesLibrary([libEntry('a', 'Catan')]);
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    expect(document.querySelector('[data-slot="games-results-grid"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="games-results-grid-link"]')).not.toBeNull();
  });

  it('renders GamesEmptyState kind=empty when library has no entries', async () => {
    hubMock.mockReturnValue(
      makeHub({ totalCounts: { games: 0, agents: 0, kb: 0, sessions: 0, chat: 0 } })
    );
    seedGamesLibrary([]);
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    const el = document.querySelector('[data-slot="games-empty-state"]');
    expect(el?.getAttribute('data-kind')).toBe('empty');
  });

  it('renders GamesEmptyState kind=filtered-empty when filter removes all', async () => {
    hubMock.mockReturnValue(
      makeHub({ totalCounts: { games: 1, agents: 0, kb: 0, sessions: 0, chat: 0 } })
    );
    seedGamesLibrary([libEntry('a', 'Catan')]);
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    // Type a non-matching query into the GamesFiltersInline search box.
    // GamesFiltersInline uses a 300ms trailing debounce; use waitFor so it settles.
    await userEvent.type(
      screen.getByRole('searchbox', { name: /cerca giochi nella tua libreria/i }),
      'xyznotfound'
    );
    await waitFor(() => {
      const el = document.querySelector('[data-slot="games-empty-state"]');
      expect(el?.getAttribute('data-kind')).toBe('filtered-empty');
    });
  });

  it('renders GamesEmptyState kind=error when libraryQuery.isError', async () => {
    hubMock.mockReturnValue(
      makeHub({ totalCounts: { games: 0, agents: 0, kb: 0, sessions: 0, chat: 0 } })
    );
    libraryMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('boom'),
    });
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    const el = document.querySelector('[data-slot="games-empty-state"]');
    expect(el?.getAttribute('data-kind')).toBe('error');
  });

  it('renders GamesEmptyState kind=loading when libraryQuery.isLoading', async () => {
    hubMock.mockReturnValue(
      makeHub({ totalCounts: { games: 0, agents: 0, kb: 0, sessions: 0, chat: 0 } })
    );
    libraryMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    });
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    const el = document.querySelector('[data-slot="games-empty-state"]');
    expect(el?.getAttribute('data-kind')).toBe('loading');
  });

  it('non-games tabs do not render the games-tab slots (regression guard for #1618)', async () => {
    // Seed a sessions item so the hybrid grid has content; games slots must be absent.
    hubMock.mockReturnValue(
      makeHub({
        sources: { games: [], agents: [], kb: [], sessions: [sessionItem()], chat: [] },
        totalCounts: { games: 0, agents: 0, kb: 0, sessions: 1, chat: 0 },
      })
    );
    renderWithIntl(<LibraryHub />);
    await userEvent.click(screen.getByRole('tab', { name: /sessioni/i }));
    expect(document.querySelector('[data-slot="games-results-grid"]')).toBeNull();
    expect(document.querySelector('[data-slot="games-empty-state"]')).toBeNull();
  });
});

// ─── Phase 3b — drawer + rail integration (#1593) ─────────────────────────
// CrossEntityFilters renders the "Più filtri" chip on non-'all' non-games tabs.
// (Games tab renders GamesFiltersInline instead; 'all' is excluded by R4 in
// CrossEntityFilters). Here we test against the 'sessions' tab which is the
// lightest non-'all' non-games tab that exercises the chip path.
describe('LibraryHub — Phase 3b drawer + rail integration (#1593)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParamsState.value = '';
    hubMock.mockReturnValue(makeHub());
    libraryMock.mockReset();
    libraryMock.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null });
    useRemoveGameFromLibraryMock.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue(undefined),
      isPending: false,
    });
    useActivityFeedMock.mockReturnValue({
      data: { items: [], count: 0 },
      isLoading: false,
      isSuccess: true,
      isError: false,
      error: null,
    });
    // Force Radix Dialog (desktop) mode for drawer tests.
    installMatchMedia(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('does not show "Più filtri" chip on the all tab (R4)', () => {
    renderWithIntl(<LibraryHub />);
    // default tab is 'all' — chip must be absent.
    expect(screen.queryByTestId('cross-entity-filters-more')).toBeNull();
  });

  it('shows "Più filtri" chip on the sessions tab', async () => {
    const user = userEvent.setup();
    renderWithIntl(<LibraryHub />);
    await user.click(screen.getByRole('tab', { name: /sessioni/i }));
    expect(screen.getByTestId('cross-entity-filters-more')).toBeInTheDocument();
  });

  it('clicking "Più filtri" chip opens the AdvancedFiltersDrawer', async () => {
    const user = userEvent.setup();
    renderWithIntl(<LibraryHub />);
    await user.click(screen.getByRole('tab', { name: /sessioni/i }));
    const chip = screen.getByTestId('cross-entity-filters-more');
    await user.click(chip);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('forwards isLoading=true from useActivityFeed to the rail (skeleton state)', () => {
    useActivityFeedMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isSuccess: false,
      isError: false,
      error: null,
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const rail = container.querySelector('[data-slot="library-activity-rail"]');
    expect(rail).not.toBeNull();
    expect(rail).toHaveAttribute('data-state', 'loading');
    const skeletons = container.querySelectorAll('[data-testid="library-activity-skeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('forwards isError=true from useActivityFeed to the rail (error state)', () => {
    useActivityFeedMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isSuccess: false,
      isError: true,
      error: new Error('500'),
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const rail = container.querySelector('[data-slot="library-activity-rail"]');
    expect(rail).toHaveAttribute('data-state', 'error');
    expect(container.querySelector('[data-testid="library-activity-error"]')).toBeInTheDocument();
  });

  it('renders cross-entity activity items in the rail when data has items', () => {
    useActivityFeedMock.mockReturnValue({
      data: {
        items: [
          {
            id: '1',
            kind: 'agent',
            entityTitle: 'Catan Tutor',
            timestamp: '2026-05-28T11:00:00+00:00',
          },
          {
            id: '2',
            kind: 'kb-indexed',
            entityTitle: 'rules.pdf',
            timestamp: '2026-05-28T10:00:00+00:00',
          },
        ],
        count: 2,
      },
      isLoading: false,
      isSuccess: true,
      isError: false,
      error: null,
    });
    const { container } = renderWithIntl(<LibraryHub />);
    const rail = container.querySelector('[data-slot="library-activity-rail"]');
    expect(rail).toHaveAttribute('data-state', 'populated');
    expect(screen.getByText('Catan Tutor')).toBeInTheDocument();
    expect(screen.getByText('rules.pdf')).toBeInTheDocument();
  });
});
