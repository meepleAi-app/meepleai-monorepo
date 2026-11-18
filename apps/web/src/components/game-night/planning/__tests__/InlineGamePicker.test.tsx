import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { InlineGamePicker } from '../InlineGamePicker';

const reportBggAttempt = vi.hoisted(() => vi.fn());
vi.mock('@/lib/images/bgg-attempt-beacon', () => ({ reportBggAttempt }));

const BGG = 'https://cf.geekdo-images.com/abc/pic1.jpg';
const ALLOWED = 'https://covers.meepleai.app/abc/cover.webp';

beforeEach(() => {
  reportBggAttempt.mockClear();
});

const mockGames = [
  { id: 'g1', title: 'Catan', thumbnailUrl: '', minPlayers: 3, maxPlayers: 4 },
  { id: 'g2', title: 'Ticket to Ride', thumbnailUrl: '', minPlayers: 2, maxPlayers: 5 },
  { id: 'g3', title: 'Gloomhaven', thumbnailUrl: '', minPlayers: 1, maxPlayers: 4 },
];

describe('InlineGamePicker', () => {
  it('renders available games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('calls onSelect when a game is clicked', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();
    render(<InlineGamePicker games={mockGames} onSelect={onSelect} />);
    await user.click(screen.getByText('Catan'));
    expect(onSelect).toHaveBeenCalledWith(mockGames[0]);
  });

  it('filters by player count', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={5} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('shows empty state when no games match', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={10} />);
    expect(screen.getByText(/nessun gioco adatto/i)).toBeInTheDocument();
  });

  it('excludes already selected games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} excludeIds={['g1']} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('never renders a BGG thumbnail: the Gamepad2 icon instead, and the beacon fires (#5072)', () => {
    const { container } = render(
      <InlineGamePicker
        games={[
          { ...mockGames[0], thumbnailUrl: BGG },
          { ...mockGames[1], thumbnailUrl: ALLOWED },
        ]}
        onSelect={vi.fn()}
      />
    );

    const catan = screen.getByRole('button', { name: /Catan/ });
    expect(container.querySelector('img[src*="geekdo-images"]')).toBeNull();
    expect(catan.querySelector('img')).toBeNull();
    expect(catan.querySelector('svg[class*="lucide-gamepad"]')).not.toBeNull();
    expect(screen.getByRole('img', { name: 'Ticket to Ride' })).toHaveAttribute('src', ALLOWED);
    expect(reportBggAttempt).toHaveBeenCalledWith(BGG);
  });
});
