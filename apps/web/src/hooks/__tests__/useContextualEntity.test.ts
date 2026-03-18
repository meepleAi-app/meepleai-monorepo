// apps/web/src/hooks/__tests__/useContextualEntity.test.ts
import { renderHook } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: vi.fn(),
}));

import { usePathname } from 'next/navigation';
import { useCardHand } from '@/stores/use-card-hand';
import { useContextualEntity } from '../useContextualEntity';

const mockUsePathname = vi.mocked(usePathname);
const mockUseCardHand = vi.mocked(useCardHand);

describe('useContextualEntity', () => {
  beforeEach(() => {
    mockUseCardHand.mockReturnValue({
      cards: [],
      focusedIdx: -1,
    } as any);
  });

  it('returns null on dashboard (no context)', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });

  it('returns game entity on game detail page', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    mockUseCardHand.mockReturnValue({
      cards: [
        { id: 'catan-123', entity: 'game', title: 'Catan', href: '/library/games/catan-123' },
      ],
      focusedIdx: 0,
    } as any);

    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(
      expect.objectContaining({
        type: 'game',
        id: 'catan-123',
        title: 'Catan',
        color: '25 95% 45%',
      })
    );
    expect(result.current?.icon).toBeDefined();
  });

  it('returns session entity on session page', () => {
    mockUsePathname.mockReturnValue('/sessions/abc-456');
    mockUseCardHand.mockReturnValue({
      cards: [{ id: 'abc-456', entity: 'session', title: 'Game Night', href: '/sessions/abc-456' }],
      focusedIdx: 0,
    } as any);

    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(
      expect.objectContaining({
        type: 'session',
        title: 'Game Night',
      })
    );
  });

  it('returns chat entity on chat page', () => {
    mockUsePathname.mockReturnValue('/chat/conv-789');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(
      expect.objectContaining({
        type: 'chatSession',
      })
    );
  });

  it('returns null on library list page', () => {
    mockUsePathname.mockReturnValue('/library');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });

  it('returns null on games catalog', () => {
    mockUsePathname.mockReturnValue('/games');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });

  it('returns null on /chat/new', () => {
    mockUsePathname.mockReturnValue('/chat/new');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });
});
