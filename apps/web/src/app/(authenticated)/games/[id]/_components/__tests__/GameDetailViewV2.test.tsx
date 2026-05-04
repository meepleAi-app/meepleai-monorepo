/**
 * Wave C.1 (Issue #581) — GameDetailViewV2 orchestrator tests.
 *
 * Verifies the 5-state FSM (`default | loading | empty | not-found | error`)
 * surface composition + `?state=` URL override matrix. Mirror the Wave B.3
 * LibraryHubV2 test pattern with i18n via IntlProvider seeded from the real
 * `pages.gameDetail.*` keys (it.json) to exercise the same react-intl path as
 * production.
 */

import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

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
  usePathname: () => '/games/g-test',
}));

// ─── Hook mocks ────────────────────────────────────────────────────────────

type MockDetailReturn = {
  data?: LibraryGameDetail | null;
  isLoading: boolean;
  isError: boolean;
  refetch?: () => void;
};

type MockAgentsReturn = {
  data?: Array<{ id: string; name: string }>;
  isLoading: boolean;
  isError: boolean;
};

const useLibraryGameDetailMock = vi.fn<[], MockDetailReturn>();
const useQueryMock = vi.fn<[], MockAgentsReturn>(() => ({
  data: [],
  isLoading: false,
  isError: false,
}));

vi.mock('@/hooks/queries/useLibrary', async () => {
  const actual = await vi.importActual<Record<string, unknown>>('@/hooks/queries/useLibrary');
  return {
    ...actual,
    useLibraryGameDetail: () => useLibraryGameDetailMock(),
  };
});

vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual<Record<string, unknown>>('@tanstack/react-query');
  return {
    ...actual,
    useQuery: () => useQueryMock(),
  };
});

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getUserAgentsForGame: vi.fn(async () => []),
    },
  },
}));

// ─── i18n loader (real keys) ──────────────────────────────────────────────

import itMessages from '@/locales/it.json';

function flatten(obj: unknown, prefix = ''): Record<string, string> {
  const out: Record<string, string> = {};
  if (obj == null || typeof obj !== 'object') return out;
  for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
    const key = prefix ? `${prefix}.${k}` : k;
    if (v && typeof v === 'object') {
      Object.assign(out, flatten(v, key));
    } else if (typeof v === 'string') {
      out[key] = v;
    }
  }
  return out;
}

const messages = flatten(itMessages);

function Wrapper({ children }: { children: React.ReactNode }): ReactElement {
  return (
    <IntlProvider locale="it" messages={messages} defaultLocale="it">
      {children}
    </IntlProvider>
  );
}

// ─── Test fixture ─────────────────────────────────────────────────────────

const FIXTURE_DETAIL: LibraryGameDetail = {
  libraryEntryId: 'lib-1',
  userId: 'u-1',
  gameId: 'g-test',
  addedAt: '2026-04-15T08:00:00.000Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: null,
  stateNotes: null,
  isAvailableForPlay: true,
  hasCustomPdf: false,
  hasRagAccess: false,
  gameTitle: 'Wingspan',
  gamePublisher: 'Stonemaier Games',
  gameYearPublished: 2019,
  gameIconUrl: null,
  gameImageUrl: null,
  description: 'Descrizione di test.',
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 70,
  complexityRating: 2.4,
  averageRating: 8.1,
  timesPlayed: 17,
  lastPlayed: '2026-04-30T19:00:00.000Z',
  winRate: '59%',
  avgDuration: '1h 18m',
  recentSessions: [],
};

// Lazy import after mocks are set up.
import { GameDetailViewV2 } from '../GameDetailViewV2';

beforeEach(() => {
  searchParamsState.value = '';
  routerPush.mockClear();
  routerReplace.mockClear();
  useLibraryGameDetailMock.mockReset();
  useQueryMock.mockReset();
  useQueryMock.mockReturnValue({ data: [], isLoading: false, isError: false });
});

describe('GameDetailViewV2 (Wave C.1)', () => {
  it('renders the loading shell when isLoading is true', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'loading');
  });

  it('renders the error shell with retry CTA when isError is true', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: vi.fn(),
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'error');
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('renders the not-found shell when data is null', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'not-found');
  });

  it('renders the default surface (hero + tabs + tabpanel) when data is loaded', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'default');
    expect(screen.getByRole('heading', { level: 1, name: 'Wingspan' })).toBeInTheDocument();
    expect(screen.getByRole('tablist')).toBeInTheDocument();
    expect(screen.getByRole('tabpanel')).toBeInTheDocument();
  });

  it('honors ?state=loading override even when data is loaded', () => {
    searchParamsState.value = 'loading';
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'loading');
  });

  it('honors ?state=not-found override', () => {
    searchParamsState.value = 'not-found';
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'not-found');
  });

  it('honors ?state=error override', () => {
    searchParamsState.value = 'error';
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const root = document.querySelector('[data-slot="game-detail-view"]');
    expect(root).toHaveAttribute('data-state', 'error');
  });

  it('renders 6 tabs in the tablist (info/rules/faqs/sessions/agents/documents)', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(6);
  });

  it('renders own variant when libraryEntryId is present', () => {
    useLibraryGameDetailMock.mockReturnValue({
      data: FIXTURE_DETAIL,
      isLoading: false,
      isError: false,
    });
    render(
      <Wrapper>
        <GameDetailViewV2 id="g-test" />
      </Wrapper>
    );
    const heroSection = document.querySelector('[data-slot="game-detail-hero"]');
    expect(heroSection).toHaveAttribute('data-variant', 'own');
  });
});
