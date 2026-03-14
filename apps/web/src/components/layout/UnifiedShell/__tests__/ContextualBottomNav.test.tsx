import { render, screen } from '@testing-library/react';
import { act, renderHook } from '@testing-library/react';
import { ContextualBottomNav } from '../ContextualBottomNav';
import { useCardHand } from '@/stores/use-card-hand';

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
}));

describe('ContextualBottomNav', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('shows default actions when no card focused', () => {
    render(<ContextualBottomNav />);
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Discover')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
  });

  it('shows entity actions when game card focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    render(<ContextualBottomNav />);
    expect(screen.getByText('Nuova Sessione')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
    expect(screen.queryByText('Library')).not.toBeInTheDocument();
  });
});
