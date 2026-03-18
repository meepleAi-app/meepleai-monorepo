// apps/web/src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts
import { act, renderHook } from '@testing-library/react';
import { useBottomNavActions } from '@/hooks/useBottomNavActions';
import { useCardHand } from '@/stores/use-card-hand';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
}));

describe('useBottomNavActions', () => {
  beforeEach(() => {
    mockPush.mockClear();
    useCardHand.setState({
      cards: [],
      focusedIdx: -1,
      pinnedIds: new Set(),
      maxHandSize: 10,
      context: 'user' as const,
      expandedStack: false,
      highlightEntity: null,
    });
    sessionStorage.clear();
  });

  it('returns empty actions when no card is focused', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current).toEqual([]);
  });

  it('returns empty actions when a non-session card is focused', () => {
    const { result: hand } = renderHook(() => useCardHand());
    act(() => {
      hand.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current).toEqual([]);
  });

  it('returns session quick actions when session card is focused', () => {
    const { result: hand } = renderHook(() => useCardHand());
    act(() => {
      hand.current.drawCard({
        id: 's1',
        entity: 'session',
        title: 'Game Night',
        href: '/sessions/s1',
      });
    });

    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual(['rules', 'faqs', 'ask-ai', 'scores']);
  });

  it('returns empty actions when focused card index is -1', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current).toHaveLength(0);
  });
});
