// apps/web/src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts
import { act, renderHook } from '@testing-library/react';
import { useBottomNavActions } from '@/hooks/use-bottom-nav-actions';
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

  it('returns default actions when no card is focused', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual(['library', 'discover', 'chat', 'sessions']);
  });

  it('returns entity-specific actions when a game card is focused', () => {
    const { result: hand } = renderHook(() => useCardHand());
    act(() => {
      hand.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual([
      'new-session',
      'wishlist',
      'chat-ai',
      'upload-pdf',
    ]);
  });

  it('returns entity-specific actions for session entity', () => {
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
    expect(result.current.map(a => a.id)).toEqual(['add-notes', 'score', 'end-session']);
  });

  it('returns default actions when focused card index is -1', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current).toHaveLength(4);
  });
});
