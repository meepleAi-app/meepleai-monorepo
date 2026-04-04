import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/hooks/useActivityFeed', () => ({
  useActivityFeed: vi.fn(),
}));

import { useActivityFeed } from '@/hooks/useActivityFeed';
import { ActivityFeed } from '../ActivityFeed';

describe('ActivityFeed', () => {
  it('mostra skeleton durante il caricamento', () => {
    vi.mocked(useActivityFeed).mockReturnValue({ items: [], isLoading: true, error: null });
    render(<ActivityFeed />);
    expect(document.querySelectorAll('[data-testid="activity-skeleton"]').length).toBeGreaterThan(
      0
    );
  });

  it('mostra gli item quando caricati', () => {
    vi.mocked(useActivityFeed).mockReturnValue({
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
    vi.mocked(useActivityFeed).mockReturnValue({ items: [], isLoading: false, error: null });
    render(<ActivityFeed />);
    expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
  });
});
