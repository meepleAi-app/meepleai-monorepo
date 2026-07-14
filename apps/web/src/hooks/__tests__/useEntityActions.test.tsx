/**
 * Tests for useEntityActions — game quick actions (Issue #2776).
 *
 * The non-'game' branches were removed as dead code in #2776 (only MeepleGameCard
 * consumes this hook, always with entity='game'); non-'game' entities now yield an
 * empty action set.
 */
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useEntityActions } from '../useEntityActions';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

vi.mock('@/hooks/useCollectionActions', () => ({
  useCollectionActions: () => ({ isInCollection: false, add: vi.fn(), remove: vi.fn() }),
}));

const mockWriteText = vi.fn<(text: string) => Promise<void>>();

const GAME_ID = '00000000-0000-4000-8000-0000000000g1';

// ============================================================================
// Tests
// ============================================================================

describe('useEntityActions — game quick actions (#2776)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockWriteText.mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: mockWriteText },
      configurable: true,
    });
  });

  it('exposes the game quick actions', () => {
    const { result } = renderHook(() =>
      useEntityActions({ entity: 'game', id: GAME_ID, userId: 'user-1' })
    );
    const labels = result.current.quickActions.map(a => a.label);
    expect(labels).toContain('Condividi');
    expect(labels).toContain('Avvia Sessione');
    expect(labels).toContain('Aggiungi a Collezione');
  });

  it('Condividi copies the game URL and shows a success toast', async () => {
    const { result } = renderHook(() =>
      useEntityActions({ entity: 'game', id: GAME_ID, userId: 'user-1' })
    );
    const share = result.current.quickActions.find(a => a.label === 'Condividi');
    if (!share) throw new Error('Condividi action not found');
    share.onClick();

    await waitFor(() =>
      expect(mockWriteText).toHaveBeenCalledWith(`${window.location.origin}/games/${GAME_ID}`)
    );
    expect(mockToastSuccess).toHaveBeenCalled();
  });

  it('returns no actions for non-game entities (dead branches removed in #2776)', () => {
    const { result } = renderHook(() =>
      useEntityActions({ entity: 'session', id: GAME_ID, userId: 'user-1' })
    );
    expect(result.current.quickActions).toEqual([]);
  });
});
