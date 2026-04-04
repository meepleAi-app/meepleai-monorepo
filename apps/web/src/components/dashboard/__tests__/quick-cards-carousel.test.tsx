import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QuickCardsCarousel } from '../quick-cards-carousel';

const mockDrawCard = vi.fn();
const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ drawCard: mockDrawCard }),
}));

const games = [
  { id: 'g1', title: 'Catan', imageUrl: '/catan.jpg', entity: 'game' as const },
  { id: 'g2', title: '7 Wonders', imageUrl: '/7w.jpg', entity: 'game' as const },
];

describe('QuickCardsCarousel', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders cards for each item', () => {
    render(<QuickCardsCarousel items={games} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('7 Wonders')).toBeInTheDocument();
  });

  it('calls drawCard and navigates on card tap', async () => {
    const user = userEvent.setup();
    render(<QuickCardsCarousel items={games} />);
    await user.click(screen.getByText('Catan'));
    expect(mockDrawCard).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'g1', entity: 'game', title: 'Catan' })
    );
    expect(mockPush).toHaveBeenCalled();
  });

  it('renders horizontally scrollable container', () => {
    render(<QuickCardsCarousel items={games} />);
    const container = screen.getByTestId('quick-cards-carousel');
    expect(container).toBeInTheDocument();
  });
});
