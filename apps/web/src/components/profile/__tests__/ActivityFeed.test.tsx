import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/hooks/useDashboardActivityFeed', () => ({
  useDashboardActivityFeed: vi.fn(),
}));

import { useDashboardActivityFeed } from '@/hooks/useDashboardActivityFeed';
import { ActivityFeed } from '../ActivityFeed';

describe('ActivityFeed', () => {
  it('mostra skeleton durante il caricamento', () => {
    vi.mocked(useDashboardActivityFeed).mockReturnValue({
      items: [],
      isLoading: true,
      error: null,
    });
    render(<ActivityFeed />);
    expect(document.querySelectorAll('[data-testid="activity-skeleton"]').length).toBeGreaterThan(
      0
    );
  });

  it('mostra gli item quando caricati', () => {
    vi.mocked(useDashboardActivityFeed).mockReturnValue({
      items: [
        {
          id: '1',
          type: 'session',
          title: 'Wingspan',
          subtitle: '3 giocatori',
          timestamp: '2026-03-30T20:00:00Z',
          iconEmoji: '🎲',
        },
        {
          id: '2',
          type: 'achievement',
          title: 'Prime Ali',
          subtitle: 'Badge sbloccato',
          timestamp: '2026-03-29T15:00:00Z',
          iconEmoji: '🏆',
        },
      ],
      isLoading: false,
      error: null,
    });
    render(<ActivityFeed />);
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Prime Ali')).toBeInTheDocument();
  });

  it('mostra stato vuoto se non ci sono attività', () => {
    vi.mocked(useDashboardActivityFeed).mockReturnValue({
      items: [],
      isLoading: false,
      error: null,
    });
    render(<ActivityFeed />);
    expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
  });
});
