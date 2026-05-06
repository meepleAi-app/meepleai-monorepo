/**
 * GamebookIndexView unit tests — SP6 Phase B Task 3 (Issue #788).
 *
 * Coverage:
 *   - 6-cell FSM rendering (loading | error | empty | default | quota-soft | quota-hard)
 *   - URL state SSOT (?fixture=) parses correctly + is gated by STATE_OVERRIDE_ENABLED
 *   - Hero CTA navigation (router.push('/gamebook/upload'))
 *   - GamebookCard click navigation only fires for status='ready'
 *   - i18n labels resolution via useTranslation (Gate A — pre-resolved before
 *     passing to pure components)
 *
 * Mocks: useGamebooks, useQuotaInfo, next/navigation, react-intl IntlProvider.
 *
 * Pattern blueprint: Wave D.3 SessionSummaryView.test.tsx.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { gamebookIndexFixtures } from '@/lib/gamebook-index';

// ─── next/navigation mock ─────────────────────────────────────────────────

const searchParamsMap: Record<string, string> = {};
const routerPush = vi.fn();
const routerReplace = vi.fn();

vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => searchParamsMap[key] ?? null,
    toString: () => new URLSearchParams(searchParamsMap).toString(),
  }),
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
}));

// ─── useGamebooks / useQuotaInfo mocks ───────────────────────────────────

type QueryReturn<T> = {
  data?: T | null;
  isPending: boolean;
  isLoading: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: Error | null;
  refetch?: () => void;
};

const useGamebooksMock = vi.fn<[], QueryReturn<unknown>>();
const useQuotaInfoMock = vi.fn<[], QueryReturn<unknown>>();

vi.mock('@/hooks/queries/useGamebooks', () => ({
  useGamebooks: () => useGamebooksMock(),
  useQuotaInfo: () => useQuotaInfoMock(),
  gamebookKeys: {
    all: ['gamebooks'],
    myGamebooks: () => ['gamebooks', 'me'],
    quota: () => ['gamebooks', 'quota'],
  },
}));

// ─── i18n messages ────────────────────────────────────────────────────────

const MESSAGES: Record<string, string> = {
  'gamebook.index.hero.title': 'I tuoi manuali',
  'gamebook.index.hero.subtitle': 'Manuali fotografati pronti per il tuo tavolo',
  'gamebook.index.hero.kpiTotalGamebooks': 'manuali',
  'gamebook.index.hero.kpiTotalSessions': 'questo mese',
  'gamebook.index.hero.kpiActiveAgents': 'traduzioni',
  'gamebook.index.hero.ctaAddManual': 'Aggiungi manuale',
  'gamebook.index.quota.title': 'Quota traduzioni',
  'gamebook.index.quota.usedCount':
    '{used, plural, =0 {0 di {total}} one {1 di {total}} other {{used} di {total}}}',
  'gamebook.index.quota.resetIn': 'Resetta il {date}',
  'gamebook.index.quota.softWarning': 'Stai per esaurire la quota mensile',
  'gamebook.index.quota.hardLimit': 'Quota raggiunta — acquista crediti per continuare',
  'gamebook.index.quota.upgrade': 'Acquista crediti',
  'gamebook.index.card.statusReady': 'Pronto',
  'gamebook.index.card.statusIndexing': 'Indicizzazione…',
  'gamebook.index.card.statusError': 'Errore · Riprova',
  'gamebook.index.card.pagesCount':
    '{pages, plural, =0 {0 pagine} one {1 pagina} other {{pages} pagine}}',
  'gamebook.index.card.chunksCount': '{count} chunks',
  'gamebook.index.card.qaCount': '{count} domande',
  'gamebook.index.card.sessionsCount': '{count} sessioni',
  'gamebook.index.card.indexingProgress': 'Indicizzazione… {indexed}/{total}',
  'gamebook.index.card.errorRetry': 'Riprova',
  'gamebook.index.card.openGamebook': 'Apri manuale',
  'gamebook.index.empty.title': 'Nessun manuale ancora',
  'gamebook.index.empty.description':
    'Fotografa il tuo primo gamebook per iniziare a giocare in italiano.',
  'gamebook.index.empty.cta': '📷 Scatta il primo manuale',
  'gamebook.index.loading.label': 'Caricamento manuali…',
  'gamebook.index.error.title': 'Impossibile caricare la libreria',
  'gamebook.index.error.description': 'Connessione instabile. Riprova fra un momento.',
  'gamebook.index.error.retry': '🔄 Riprova',
};

// ─── Test renderer ────────────────────────────────────────────────────────

import { GamebookIndexView } from '../GamebookIndexView';

function renderView(): ReactElement {
  return render(
    <IntlProvider locale="it" messages={MESSAGES} defaultLocale="it">
      <GamebookIndexView />
    </IntlProvider>
  );
}

// ─── Hook helpers ─────────────────────────────────────────────────────────

function setHookSuccess(
  gamebooksData = gamebookIndexFixtures.default.gamebooks,
  quotaData = gamebookIndexFixtures.default.quota
): void {
  useGamebooksMock.mockReturnValue({
    data: gamebooksData,
    isPending: false,
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
    refetch: vi.fn(),
  });
  useQuotaInfoMock.mockReturnValue({
    data: quotaData,
    isPending: false,
    isLoading: false,
    isError: false,
    isSuccess: true,
    error: null,
    refetch: vi.fn(),
  });
}

// ─── Setup ────────────────────────────────────────────────────────────────

beforeEach(() => {
  Object.keys(searchParamsMap).forEach(k => delete searchParamsMap[k]);
  routerPush.mockClear();
  routerReplace.mockClear();
  useGamebooksMock.mockReset();
  useQuotaInfoMock.mockReset();
  // Default: success with default fixture data (most tests want this)
  setHookSuccess();
});

// ─── FSM cell tests ───────────────────────────────────────────────────────

describe('GamebookIndexView — 6-cell FSM rendering', () => {
  it('renders loading shell when gamebooksQuery isPending', () => {
    useGamebooksMock.mockReturnValue({
      data: undefined,
      isPending: true,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'loading');
    // Skeleton grid should render 6 placeholders
    expect(document.querySelectorAll('[data-slot="gamebook-card-skeleton"]')).toHaveLength(6);
  });

  it('renders loading shell when quotaQuery isPending (gamebooks ready)', () => {
    useQuotaInfoMock.mockReturnValue({
      data: undefined,
      isPending: true,
      isLoading: true,
      isError: false,
      isSuccess: false,
      error: null,
    });
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'loading');
  });

  it('renders error shell when gamebooksQuery errored', () => {
    useGamebooksMock.mockReturnValue({
      data: null,
      isPending: false,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('Network 500'),
    });
    renderView();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Impossibile caricare la libreria')).toBeInTheDocument();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'error');
  });

  it('renders empty cell when gamebooks list is empty', () => {
    setHookSuccess([], gamebookIndexFixtures.empty.quota);
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'empty');
    expect(document.querySelector('[data-slot="empty-gamebooks"]')).toBeInTheDocument();
    expect(screen.getByText('Nessun manuale ancora')).toBeInTheDocument();
  });

  it('renders default cell when gamebooks present and quota healthy', () => {
    setHookSuccess(); // default fixture: 4 gamebooks, quota 12/50
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'default');
    expect(document.querySelectorAll('[data-slot="gamebook-card"]').length).toBeGreaterThan(0);
    expect(document.querySelector('[data-slot="quota-widget"]')).toHaveAttribute(
      'data-variant',
      'default'
    );
  });

  it('renders quota-soft cell when ratio ≥ 0.9 and < 1.0', () => {
    setHookSuccess(
      gamebookIndexFixtures['quota-soft'].gamebooks,
      gamebookIndexFixtures['quota-soft'].quota
    );
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'quota-soft');
    expect(document.querySelector('[data-slot="quota-widget"]')).toHaveAttribute(
      'data-variant',
      'soft'
    );
    expect(document.querySelector('[data-slot="quota-widget-soft-banner"]')).toBeInTheDocument();
  });

  it('renders quota-hard cell when used >= total', () => {
    setHookSuccess(
      gamebookIndexFixtures['quota-hard'].gamebooks,
      gamebookIndexFixtures['quota-hard'].quota
    );
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'quota-hard');
    expect(document.querySelector('[data-slot="quota-widget"]')).toHaveAttribute(
      'data-variant',
      'hard'
    );
    expect(document.querySelector('[data-slot="quota-widget-hard-banner"]')).toBeInTheDocument();
  });
});

// ─── URL state SSOT (fixture override) ─────────────────────────────────────

describe('GamebookIndexView — URL fixture override', () => {
  it('?fixture=empty renders the empty cell regardless of hook data', () => {
    // Even though the default hook returns 4 gamebooks, the override forces empty.
    searchParamsMap.fixture = 'empty';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'empty');
    expect(document.querySelector('[data-slot="empty-gamebooks"]')).toBeInTheDocument();
  });

  it('?fixture=quota-hard renders the quota-hard cell with banner', () => {
    searchParamsMap.fixture = 'quota-hard';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'quota-hard');
    expect(document.querySelector('[data-slot="quota-widget-hard-banner"]')).toBeInTheDocument();
  });

  it('?fixture=quota-soft renders the quota-soft cell with warning banner', () => {
    searchParamsMap.fixture = 'quota-soft';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'quota-soft');
    expect(document.querySelector('[data-slot="quota-widget-soft-banner"]')).toBeInTheDocument();
  });

  it('?fixture=loading renders skeleton even when hooks resolved', () => {
    searchParamsMap.fixture = 'loading';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'loading');
    expect(document.querySelectorAll('[data-slot="gamebook-card-skeleton"]')).toHaveLength(6);
  });

  it('?fixture=error renders error shell even when hooks resolved', () => {
    searchParamsMap.fixture = 'error';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'error');
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('?fixture=invalid is ignored and falls through to real hook data', () => {
    searchParamsMap.fixture = 'gibberish';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    // Real-data path uses default fixture → default cell
    expect(root).toHaveAttribute('data-ui-state', 'default');
  });

  it('?fixture=default renders default cell with grid', () => {
    searchParamsMap.fixture = 'default';
    renderView();
    const root = document.querySelector('[data-slot="gamebook-index-view"]');
    expect(root).toHaveAttribute('data-ui-state', 'default');
    expect(document.querySelector('[data-slot="gamebook-index-grid"]')).toBeInTheDocument();
  });
});

// ─── Navigation tests ─────────────────────────────────────────────────────

describe('GamebookIndexView — navigation', () => {
  it('Hero CTA click navigates to /gamebook/upload (default cell)', () => {
    setHookSuccess();
    renderView();
    const cta = document.querySelector(
      '[data-slot="gamebook-hero-cta"]'
    ) as HTMLButtonElement | null;
    expect(cta).not.toBeNull();
    fireEvent.click(cta!);
    expect(routerPush).toHaveBeenCalledWith('/gamebook/upload');
  });

  it('Hero CTA click in empty cell ALSO navigates to /gamebook/upload', () => {
    setHookSuccess([], gamebookIndexFixtures.empty.quota);
    renderView();
    const cta = document.querySelector(
      '[data-slot="gamebook-hero-cta"]'
    ) as HTMLButtonElement | null;
    fireEvent.click(cta!);
    expect(routerPush).toHaveBeenCalledWith('/gamebook/upload');
  });

  it('Empty state CTA navigates to /gamebook/upload', () => {
    setHookSuccess([], gamebookIndexFixtures.empty.quota);
    renderView();
    const cta = document.querySelector(
      '[data-slot="empty-gamebooks-cta"]'
    ) as HTMLButtonElement | null;
    expect(cta).not.toBeNull();
    fireEvent.click(cta!);
    expect(routerPush).toHaveBeenCalledWith('/gamebook/upload');
  });

  it('GamebookCard click (status=ready) navigates to /gamebook/[gameId]', () => {
    setHookSuccess();
    renderView();
    // Pick the first ready gamebook (Nanolith)
    const readyCards = document.querySelectorAll(
      '[data-slot="gamebook-card"][data-status="ready"]'
    );
    expect(readyCards.length).toBeGreaterThan(0);
    const firstReadyId = (readyCards[0] as HTMLElement).getAttribute('data-gamebook-id');
    expect(firstReadyId).toBeTruthy();
    // The card is a button when ready — click it.
    fireEvent.click(readyCards[0] as HTMLElement);
    // Expected gameId from default fixture: Nanolith → '00000000-0000-4000-8000-0000000c0001'
    expect(routerPush).toHaveBeenCalledWith(expect.stringMatching(/^\/gamebook\//));
    // Specifically, the first ready gamebook is Nanolith.
    const expectedGameId = gamebookIndexFixtures.default.gamebooks[0].gameId;
    expect(routerPush).toHaveBeenCalledWith(`/gamebook/${expectedGameId}`);
  });

  it('GamebookCard click does NOT fire push for status=indexing card', () => {
    setHookSuccess();
    renderView();
    const indexingCard = document.querySelector(
      '[data-slot="gamebook-card"][data-status="indexing"]'
    );
    expect(indexingCard).not.toBeNull();
    // Indexing card is rendered as <article>, not a clickable button.
    // Click handler is wired only for ready status — fireEvent.click should
    // not trigger router.push.
    fireEvent.click(indexingCard as HTMLElement);
    expect(routerPush).not.toHaveBeenCalled();
  });

  it('GamebookCard click does NOT fire game navigation for status=error', () => {
    setHookSuccess();
    renderView();
    const errorCard = document.querySelector('[data-slot="gamebook-card"][data-status="error"]');
    expect(errorCard).not.toBeNull();
    // Error card is <article>, not clickable. Clicking the card itself
    // should NOT navigate. (The error pill retry button is a separate
    // affordance — `gamebook-card-retry-cta` — handled inside the card.)
    fireEvent.click(errorCard as HTMLElement);
    expect(routerPush).not.toHaveBeenCalledWith(expect.stringMatching(/^\/gamebook\/[0-9a-f-]+$/));
  });

  it('Hero CTA in quota-hard cell triggers upgrade handler (no navigation to upload)', () => {
    setHookSuccess(
      gamebookIndexFixtures['quota-hard'].gamebooks,
      gamebookIndexFixtures['quota-hard'].quota
    );
    renderView();
    const cta = document.querySelector(
      '[data-slot="gamebook-hero-cta"]'
    ) as HTMLButtonElement | null;
    fireEvent.click(cta!);
    // Upgrade handler is no-op in v1 → router.push must NOT have been called.
    expect(routerPush).not.toHaveBeenCalled();
  });

  it('Error retry button refetches both queries', () => {
    const refetchGamebooks = vi.fn();
    const refetchQuota = vi.fn();
    useGamebooksMock.mockReturnValue({
      data: null,
      isPending: false,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('Network 500'),
      refetch: refetchGamebooks,
    });
    useQuotaInfoMock.mockReturnValue({
      data: null,
      isPending: false,
      isLoading: false,
      isError: false,
      isSuccess: false,
      error: null,
      refetch: refetchQuota,
    });
    renderView();
    const retry = document.querySelector(
      '[data-slot="gamebook-index-error-retry"]'
    ) as HTMLButtonElement | null;
    expect(retry).not.toBeNull();
    fireEvent.click(retry!);
    expect(refetchGamebooks).toHaveBeenCalled();
    expect(refetchQuota).toHaveBeenCalled();
  });
});

// ─── i18n labels resolution ───────────────────────────────────────────────

describe('GamebookIndexView — i18n labels (Gate A)', () => {
  it('hero displays resolved Italian title', () => {
    setHookSuccess();
    renderView();
    expect(screen.getByText('I tuoi manuali')).toBeInTheDocument();
  });

  it('hero subtitle resolved from useTranslation', () => {
    setHookSuccess();
    renderView();
    expect(screen.getByText('Manuali fotografati pronti per il tuo tavolo')).toBeInTheDocument();
  });

  it('quota usedLabel resolves ICU plural "12 di 50"', () => {
    setHookSuccess();
    renderView();
    const counter = document.querySelector('[data-slot="quota-widget-counter"]');
    expect(counter?.textContent).toBe('12 di 50');
  });

  it('empty state resolves Italian description', () => {
    setHookSuccess([], gamebookIndexFixtures.empty.quota);
    renderView();
    expect(
      screen.getByText('Fotografa il tuo primo gamebook per iniziare a giocare in italiano.')
    ).toBeInTheDocument();
  });

  it('error shell resolves Italian title and retry CTA', () => {
    useGamebooksMock.mockReturnValue({
      data: null,
      isPending: false,
      isLoading: false,
      isError: true,
      isSuccess: false,
      error: new Error('boom'),
    });
    renderView();
    expect(screen.getByText('Impossibile caricare la libreria')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="gamebook-index-error-retry"]')?.textContent).toBe(
      '🔄 Riprova'
    );
  });
});

// ─── KPI counts ───────────────────────────────────────────────────────────

describe('GamebookIndexView — KPI computation', () => {
  it('hero KPIs sum from default fixture (4 gamebooks, 5 sessions, 19 Q&A)', () => {
    // Default fixture sessionsCount: 3 + 2 + 0 + 0 = 5
    // Default fixture qaCount: 12 + 7 + 0 + 0 = 19
    setHookSuccess();
    renderView();
    const totalGamebooks = document.querySelector(
      '[data-slot="gamebook-hero-kpi-totalGamebooks"] dd'
    );
    const totalSessions = document.querySelector(
      '[data-slot="gamebook-hero-kpi-totalSessions"] dd'
    );
    const activeAgents = document.querySelector('[data-slot="gamebook-hero-kpi-activeAgents"] dd');
    expect(totalGamebooks?.textContent).toBe('4');
    expect(totalSessions?.textContent).toBe('5');
    expect(activeAgents?.textContent).toBe('19');
  });

  it('hero KPIs are zero in empty cell', () => {
    setHookSuccess([], gamebookIndexFixtures.empty.quota);
    renderView();
    const totalGamebooks = document.querySelector(
      '[data-slot="gamebook-hero-kpi-totalGamebooks"] dd'
    );
    expect(totalGamebooks?.textContent).toBe('0');
  });
});
