import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { GameNightList } from '../GameNightList';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const mockNights = [
  {
    id: '1',
    title: 'Friday',
    status: 'upcoming' as const,
    date: '2026-03-15T19:00:00Z',
    playerCount: 4,
    gameCount: 2,
    playerAvatars: [],
    gameThumbnails: [],
  },
  {
    id: '2',
    title: 'Saturday',
    status: 'draft' as const,
    date: '2026-03-16T18:00:00Z',
    playerCount: 0,
    gameCount: 0,
    playerAvatars: [],
    gameThumbnails: [],
  },
];

describe('GameNightList', () => {
  it('renders a grid of game night cards', () => {
    render(<GameNightList nights={mockNights} isLoading={false} />);
    expect(screen.getByText('Friday')).toBeInTheDocument();
    expect(screen.getByText('Saturday')).toBeInTheDocument();
  });

  it('renders skeleton when loading', () => {
    render(<GameNightList nights={[]} isLoading={true} />);
    expect(screen.getAllByTestId('game-night-skeleton')).toHaveLength(3);
  });

  it('renders empty state when no nights', () => {
    render(<GameNightList nights={[]} isLoading={false} />);
    expect(screen.getByText(/nessuna serata pianificata/i)).toBeInTheDocument();
  });
});
