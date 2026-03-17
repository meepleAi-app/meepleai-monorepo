import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GameNightCard } from '../GameNightCard';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const mockNight = {
  id: 'gn-1',
  title: 'Venerdì Ludico',
  status: 'upcoming' as const,
  date: '2026-03-15T19:00:00Z',
  location: 'Casa di Marco',
  playerCount: 4,
  gameCount: 3,
  playerAvatars: [],
  gameThumbnails: [],
};

describe('GameNightCard', () => {
  it('renders the title', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText('Venerdì Ludico')).toBeInTheDocument();
  });

  it('shows upcoming status badge', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText(/prossima/i)).toBeInTheDocument();
  });

  it('shows draft status badge', () => {
    render(<GameNightCard night={{ ...mockNight, status: 'draft' }} />);
    expect(screen.getByText(/bozza/i)).toBeInTheDocument();
  });

  it('shows completed status with dimmed style', () => {
    render(<GameNightCard night={{ ...mockNight, status: 'completed' }} />);
    expect(screen.getByText(/completata/i)).toBeInTheDocument();
  });

  it('links to the game night detail page', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/game-nights/gn-1');
  });

  it('shows location when provided', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText('Casa di Marco')).toBeInTheDocument();
  });

  it('shows player and game counts', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText(/4/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });
});
