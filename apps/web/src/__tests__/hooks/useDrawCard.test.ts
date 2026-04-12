import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mockDrawCard = vi.fn();
vi.mock('@/lib/stores/card-hand-store', () => ({
  useCardHand: (selector: (s: { drawCard: typeof mockDrawCard }) => unknown) =>
    selector({ drawCard: mockDrawCard }),
}));

import { useDrawCard } from '@/hooks/useDrawCard';

describe('useDrawCard', () => {
  beforeEach(() => mockDrawCard.mockClear());

  it('calls drawCard on mount with the provided card', () => {
    renderHook(() =>
      useDrawCard({
        id: 'game:abc',
        entityType: 'game',
        entityId: 'abc',
        label: 'Catan',
        href: '/games/abc',
        pinned: false,
      })
    );
    expect(mockDrawCard).toHaveBeenCalledOnce();
    expect(mockDrawCard).toHaveBeenCalledWith(
      expect.objectContaining({ entityType: 'game', entityId: 'abc' })
    );
  });

  it('does not call drawCard when card is null', () => {
    renderHook(() => useDrawCard(null));
    expect(mockDrawCard).not.toHaveBeenCalled();
  });
});
