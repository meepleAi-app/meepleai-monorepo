import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QuickCardsCarousel } from '../quick-cards-carousel';
import { useRecentsStore } from '@/stores/use-recents';

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const games = [
  { id: 'g1', title: 'Catan', imageUrl: '/catan.jpg', entity: 'game' as const },
  { id: 'g2', title: '7 Wonders', imageUrl: '/7w.jpg', entity: 'game' as const },
];

describe('QuickCardsCarousel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useRecentsStore.setState({ items: [] });
  });

  it('renders cards for each item', () => {
    render(<QuickCardsCarousel items={games} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('7 Wonders')).toBeInTheDocument();
  });

  it('pushes to items store and navigates on card tap', async () => {
    const user = userEvent.setup();
    render(<QuickCardsCarousel items={games} />);
    await user.click(screen.getByText('Catan'));
    const items = useRecentsStore.getState().items;
    expect(items).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ id: 'g1', entity: 'game', title: 'Catan' }),
      ])
    );
    expect(mockPush).toHaveBeenCalled();
  });

  it('renders horizontally scrollable container', () => {
    render(<QuickCardsCarousel items={games} />);
    const container = screen.getByTestId('quick-cards-carousel');
    expect(container).toBeInTheDocument();
  });
});
