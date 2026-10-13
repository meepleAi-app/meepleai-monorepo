import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, render } from '@testing-library/react';

import { GameNightDetailView } from '../GameNightDetailView';

const detailMock = vi.fn();
const storeAddGame = vi.hoisted(() => vi.fn());
const captured = vi.hoisted(() => ({
  voting: {} as Record<string, unknown>,
  planning: {} as Record<string, unknown>,
}));
const getTabMock = vi.fn<(key: string) => string | null>();

vi.mock('@/hooks/queries/useGameNightDetail', () => ({
  useGameNightDetail: () => detailMock(),
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'org-1' } }),
}));
vi.mock('@/hooks/queries/useGameNights', () => ({
  usePublishGameNight: () => ({ mutate: vi.fn(), isPending: false }),
  useCancelGameNight: () => ({ mutate: vi.fn(), isPending: false }),
}));
// #4187: la prima pagina del catalogo NON contiene il gioco della serata.
const catalog = vi.hoisted(() => ({
  listArgs: [] as unknown[][],
  byIdsArgs: [] as unknown[][],
}));
vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: (...args: unknown[]) => {
    catalog.listArgs.push(args);
    return {
      data: {
        items: [{ id: 'g1', title: 'Azul', minPlayers: 2, maxPlayers: 4, thumbnailUrl: '' }],
      },
    };
  },
  useSharedGamesByIds: (ids: string[], enabled?: boolean) => {
    catalog.byIdsArgs.push([ids, enabled]);
    const known: Record<string, unknown> = {
      g42: { id: 'g42', title: 'Wingspan', minPlayers: 1, maxPlayers: 5, thumbnailUrl: '' },
    };
    return {
      gamesById: Object.fromEntries(ids.filter(id => known[id]).map(id => [id, known[id]])),
      isLoading: false,
    };
  },
}));
vi.mock('@/stores/game-night', () => ({
  useGameNightStore: () => ({
    addPlayer: vi.fn(),
    addGame: storeAddGame,
    reset: vi.fn(),
    activeSessions: [],
  }),
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it-IT' }),
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => ({ get: getTabMock }),
}));

vi.mock('@/components/features/game-night-detail', () => ({
  GameNightDetailHero: () => <div data-testid="hero" />,
  GameNightCancelledBanner: () => null,
  GameNightRsvpActionBar: () => null,
  GameNightRsvpRow: () => <div data-testid="rsvp-row" />,
}));
vi.mock('@/components/game-night/GameNightActions', () => ({
  GameNightActions: () => <div data-testid="game-night-actions" />,
}));
vi.mock('@/components/game-night/GameNightSessionsList', () => ({
  GameNightSessionsList: () => <div data-testid="sessions-list" />,
}));
vi.mock('@/components/game-night/GameNightDiaryPanel', () => ({
  GameNightDiaryPanel: () => <div data-testid="diary-panel" />,
}));
vi.mock('@/components/game-night/planning/GameNightPlanningLayout', () => ({
  GameNightPlanningLayout: (props: Record<string, unknown>) => {
    captured.planning = props;
    return null;
  },
}));
vi.mock('../GameNightEditDrawer', () => ({ GameNightEditDrawer: () => null }));
vi.mock('@/components/features/game-night-detail/voting/VotingPanel', () => ({
  VotingPanel: (props: Record<string, unknown>) => {
    captured.voting = props;
    return <div data-testid="voting-panel" />;
  },
}));

function event(status: 'Draft' | 'Published', gameIds: string[]) {
  return {
    id: 'gn-1',
    status,
    organizerId: 'org-1',
    organizerName: 'Marco',
    title: 'Serata',
    description: null,
    scheduledAt: '2026-08-01T20:00:00.000Z',
    location: null,
    maxPlayers: null,
    gameIds,
    acceptedCount: 1,
  };
}

function withEvent(status: 'Draft' | 'Published', gameIds: string[]) {
  detailMock.mockReturnValue({
    event: event(status, gameIds),
    rsvps: [{ id: 'r1', userId: 'org-1', userName: 'Marco', status: 'Accepted' }],
    actor: { actor: 'host' },
    isLoading: false,
    isError: false,
    currentResponse: undefined,
    pendingResponse: null,
    submitRsvp: vi.fn(),
    isSubmitting: false,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  catalog.listArgs.length = 0;
  catalog.byIdsArgs.length = 0;
  captured.voting = {};
  captured.planning = {};
  getTabMock.mockReturnValue(null);
});

// #4187: i giochi della serata si leggono per id. Prima si cercavano nella prima pagina del
// catalogo (20 giochi), e quelli oltre sparivano.
describe('GameNightDetailView — games beyond the first catalog page (#4187)', () => {
  it('names a candidate game in the voting panel even when it is not on the first page', () => {
    getTabMock.mockImplementation(k => (k === 'tab' ? 'voting' : null));
    withEvent('Published', ['g42']);

    render(<GameNightDetailView id="gn-1" />);

    expect(captured.voting.gameTitleById).toMatchObject({ g42: 'Wingspan' });
  });

  it('puts a planned game in the draft even when it is not on the first page', () => {
    withEvent('Draft', ['g42']);

    render(<GameNightDetailView id="gn-1" />);

    expect(storeAddGame).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'g42', title: 'Wingspan' })
    );
  });

  it('searches the catalog on the server from the planning picker, after a short pause', () => {
    vi.useFakeTimers();
    try {
      withEvent('Draft', []);
      render(<GameNightDetailView id="gn-1" />);

      const onSearchChange = captured.planning.onCatalogSearchChange as (term: string) => void;
      expect(onSearchChange).toBeTypeOf('function');
      act(() => onSearchChange('Wingspan'));
      expect(captured.planning.catalogSearch).toBe('Wingspan');
      // Il debounce evita una richiesta per ogni tasto.
      expect(catalog.listArgs.at(-1)?.[0]).toBeUndefined();

      act(() => vi.advanceTimersByTime(300));

      expect(catalog.listArgs.at(-1)?.[0]).toMatchObject({ searchTerm: 'Wingspan' });
    } finally {
      vi.useRealTimers();
    }
  });
});
