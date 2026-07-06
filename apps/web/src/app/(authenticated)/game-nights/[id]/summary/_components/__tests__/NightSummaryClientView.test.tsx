import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { NightSummaryClientView } from '../NightSummaryClientView';

const summaryMock = vi.fn();
const generateMock = vi.fn();
const archiveMock = vi.fn();

vi.mock('@/hooks/queries/useGameNights', () => ({
  useGameNightSummary: () => summaryMock(),
  useGenerateGameNightShareToken: () => ({ mutate: generateMock }),
  useSetGameNightArchived: () => ({ mutate: archiveMock }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key, locale: 'it-IT' }),
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

// Stub the heavy presentational view; capture the adapted props.
vi.mock('@/components/features/game-nights/summary', () => ({
  NightSummaryView: (props: {
    night: { title: string };
    mvp?: { name: string } | null;
    games: unknown[];
    archived?: boolean;
    onShare?: () => void;
    onArchive?: () => void;
  }) => (
    <div
      data-testid="night-summary-view"
      data-title={props.night.title}
      data-mvp={props.mvp?.name ?? ''}
      data-games={props.games.length}
      data-archived={String(props.archived)}
    >
      <button type="button" data-testid="share-btn" onClick={props.onShare}>
        share
      </button>
      <button type="button" data-testid="archive-btn" onClick={props.onArchive}>
        archive
      </button>
    </div>
  ),
}));

const dto = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Serata Test',
  status: 'Completed',
  scheduledAt: '2026-08-01T20:00:00.000Z',
  location: 'Casa',
  isViewerOrganizer: true,
  isArchived: false,
  isShared: false,
  shareToken: null as string | null,
  mvp: { playerName: 'Davide', wins: 2 },
  kpis: { totalGames: 2, completedGames: 2, totalDurationMinutes: 150, winnersCount: 2 },
  games: [
    {
      sessionId: 's1',
      gameId: 'g1',
      gameTitle: 'Brass',
      playOrder: 1,
      status: 'Completed',
      winnerId: null,
      winnerName: 'Davide',
      durationMinutes: 90,
      eventsCount: 8,
      topPlayers: [
        { playerName: 'Davide', rank: 1 },
        { playerName: 'Marco', rank: 2 },
      ],
    },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
  summaryMock.mockReturnValue({ data: dto, isLoading: false, isError: false });
});

describe('NightSummaryClientView', () => {
  it('renders the loading state', () => {
    summaryMock.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<NightSummaryClientView nightId="gn-1" />);
    expect(screen.getByTestId('summary-loading')).toBeInTheDocument();
  });

  it('renders the error state', () => {
    summaryMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    render(<NightSummaryClientView nightId="gn-1" />);
    expect(screen.getByTestId('summary-error')).toBeInTheDocument();
  });

  it('adapts the summary DTO into the view props', () => {
    render(<NightSummaryClientView nightId="gn-1" />);
    const view = screen.getByTestId('night-summary-view');
    expect(view).toHaveAttribute('data-title', 'Serata Test');
    expect(view).toHaveAttribute('data-mvp', 'Davide');
    expect(view).toHaveAttribute('data-games', '1');
    expect(view).toHaveAttribute('data-archived', 'false');
  });

  it('generates a share token when sharing', async () => {
    const user = userEvent.setup();
    render(<NightSummaryClientView nightId="gn-1" />);
    await user.click(screen.getByTestId('share-btn'));
    expect(generateMock).toHaveBeenCalledWith('gn-1', expect.any(Object));
  });

  it('archives the night when archiving', async () => {
    const user = userEvent.setup();
    render(<NightSummaryClientView nightId="gn-1" />);
    await user.click(screen.getByTestId('archive-btn'));
    expect(archiveMock).toHaveBeenCalledWith({ id: 'gn-1', archived: true });
  });
});
