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

  it('renders empty nav when no card focused', () => {
    render(<ContextualBottomNav />);
    const nav = screen.getByTestId('contextual-bottom-nav');
    expect(nav).toBeInTheDocument();
    // No actions rendered when no card focused
    expect(nav.children).toHaveLength(0);
  });

  it('shows session actions when session card focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({
        id: 's1',
        entity: 'session',
        title: 'Game Night',
        href: '/sessions/s1',
      });
    });

    render(<ContextualBottomNav />);
    expect(screen.getByText('Regole')).toBeInTheDocument();
    expect(screen.getByText('FAQ')).toBeInTheDocument();
    expect(screen.getByText('Chiedi AI')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
  });

  it('renders empty nav when non-session card focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    render(<ContextualBottomNav />);
    const nav = screen.getByTestId('contextual-bottom-nav');
    expect(nav.children).toHaveLength(0);
  });
});
