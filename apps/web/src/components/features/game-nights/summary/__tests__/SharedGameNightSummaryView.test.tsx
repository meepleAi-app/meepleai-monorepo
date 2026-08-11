import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { SharedGameNightSummaryView } from '../SharedGameNightSummaryView';

const sharedMock = vi.fn();
const sharedPhotosMock = vi.fn();

vi.mock('@/hooks/queries/useGameNights', () => ({
  useSharedGameNightSummary: () => sharedMock(),
  useSharedGameNightPhotos: () => sharedPhotosMock(),
}));

vi.mock('../../photos/GameNightPhotoGallery', () => ({
  GameNightPhotoGallery: () => <div data-testid="photo-gallery" />,
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key, locale: 'it-IT' }),
}));

vi.mock('../NightSummaryView', () => ({
  NightSummaryView: (props: {
    night: { title: string };
    onShare?: () => void;
    onArchive?: () => void;
  }) => (
    <div
      data-testid="night-summary-view"
      data-title={props.night.title}
      data-has-share={String(Boolean(props.onShare))}
      data-has-archive={String(Boolean(props.onArchive))}
    />
  ),
}));

const dto = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Serata Condivisa',
  status: 'Completed',
  scheduledAt: '2026-08-01T20:00:00.000Z',
  location: null as string | null,
  isViewerOrganizer: false,
  isArchived: false,
  isShared: true,
  shareToken: null as string | null,
  mvp: null as { playerName: string; wins: number } | null,
  kpis: { totalGames: 1, completedGames: 1, totalDurationMinutes: 60, winnersCount: 0 },
  games: [],
};

beforeEach(() => {
  vi.clearAllMocks();
  sharedMock.mockReturnValue({ data: dto, isLoading: false, isError: false });
  sharedPhotosMock.mockReturnValue({ data: [], isLoading: false, isError: false });
});

describe('SharedGameNightSummaryView', () => {
  it('renders loading', () => {
    sharedMock.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<SharedGameNightSummaryView token="tok" />);
    expect(screen.getByTestId('shared-summary-loading')).toBeInTheDocument();
  });

  it('renders error / not-found', () => {
    sharedMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    render(<SharedGameNightSummaryView token="tok" />);
    expect(screen.getByTestId('shared-summary-error')).toBeInTheDocument();
  });

  it('renders the summary read-only (no share/archive CTAs)', () => {
    render(<SharedGameNightSummaryView token="tok" />);
    const view = screen.getByTestId('night-summary-view');
    expect(view).toHaveAttribute('data-title', 'Serata Condivisa');
    expect(view).toHaveAttribute('data-has-share', 'false');
    expect(view).toHaveAttribute('data-has-archive', 'false');
  });
});
