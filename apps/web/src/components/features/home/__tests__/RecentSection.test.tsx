import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { RecentSection } from '../RecentSection';

const mockUseCompleted = vi.fn();
vi.mock('@/hooks/queries/useGameNights', () => ({
  useCompletedGameNights: (opts: unknown) => mockUseCompleted(opts),
}));

// MeepleEventCard uses useRouter internally — stub it to keep this a unit test.
vi.mock('@/components/game-night/MeepleEventCard', () => ({
  MeepleEventCard: ({ event }: { event: { id: string; title: string } }) => (
    <div data-testid={`event-card-${event.id}`}>{event.title}</div>
  ),
}));

const baseProps = { onOpenDetail: () => {}, onSeeAll: () => {} };

describe('RecentSection', () => {
  beforeEach(() => mockUseCompleted.mockReset());

  it('renders completed game-nights with the "Recenti" heading', () => {
    mockUseCompleted.mockReturnValue({
      data: [
        {
          id: 'gn-thu',
          title: 'Giovedì Wingspan',
          scheduledAt: '2026-07-09T20:00:00Z',
          location: 'Casa Anna',
        },
      ],
      isLoading: false,
      isError: false,
    });
    render(<RecentSection {...baseProps} />);
    const section = screen.getByTestId('recent-section');
    expect(within(section).getByText('Recenti')).toBeInTheDocument();
    expect(within(section).getByText('Giovedì Wingspan')).toBeInTheDocument();
    expect(
      within(section).getByRole('button', { name: /Vedi tutte le completate/i })
    ).toBeInTheDocument();
  });

  it('renders empty state when there are no completed game-nights', () => {
    mockUseCompleted.mockReturnValue({ data: [], isLoading: false, isError: false });
    render(<RecentSection {...baseProps} />);
    expect(screen.getByText('Nessuna partita ancora')).toBeInTheDocument();
  });

  it('renders a skeleton while loading (no spinner)', () => {
    mockUseCompleted.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<RecentSection {...baseProps} />);
    expect(screen.getByTestId('recent-section-skeleton')).toBeInTheDocument();
  });
});
