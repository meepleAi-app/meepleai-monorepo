import { render, screen } from '@testing-library/react';
import { act, renderHook } from '@testing-library/react';
import { UnifiedTopNav } from '../UnifiedTopNav';
import { useCardHand } from '@/stores/use-card-hand';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/library',
}));

describe('UnifiedTopNav', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('renders logo', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.getByTestId('unified-top-nav')).toBeInTheDocument();
  });

  it('shows app name when no card focused', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    // Logo + center fallback both render "MeepleAI"
    const matches = screen.getAllByText('MeepleAI');
    expect(matches.length).toBeGreaterThanOrEqual(2);
  });

  it('shows focused card title when card is focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('hides admin toggle for non-admin users', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.queryByTestId('admin-toggle')).not.toBeInTheDocument();
  });

  it('shows admin toggle for admin users', () => {
    render(<UnifiedTopNav isAdmin={true} />);
    expect(screen.getByTestId('admin-toggle')).toBeInTheDocument();
  });
});
