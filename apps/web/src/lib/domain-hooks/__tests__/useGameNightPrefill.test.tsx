/**
 * useGameNightPrefill Hook Tests — #2348 GameNight prefill composing hook
 *
 * Verifies that a completed GameNight's data (date/location/game/roster) is
 * mapped to ready-to-spread initialValues/initialPlayers for SessionCreateForm.
 */

import { createElement, type ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { useGameNightPrefill } from '../useGameNightPrefill';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  api: {
    gameNights: {
      getById: vi.fn().mockResolvedValue({
        id: 'gn-1',
        organizerId: 'u-org',
        organizerName: 'Org',
        title: 'Sabato',
        description: null,
        scheduledAt: '2026-05-17T20:00:00.000Z',
        location: 'Padova',
        maxPlayers: 6,
        gameIds: ['game-1'],
        status: 'Completed',
        acceptedCount: 2,
        pendingCount: 0,
        totalInvited: 2,
        createdAt: '2026-05-01T00:00:00.000Z',
      }),
      getRsvps: vi.fn().mockResolvedValue([
        {
          id: 'r1',
          userId: 'u-1',
          userName: 'Marco',
          status: 'Accepted',
          respondedAt: null,
          createdAt: '2026-05-02T00:00:00.000Z',
        },
        {
          id: 'r2',
          userId: 'u-2',
          userName: 'Davide',
          status: 'Declined',
          respondedAt: null,
          createdAt: '2026-05-02T00:00:00.000Z',
        },
      ]),
    },
    games: {
      getById: vi.fn().mockResolvedValue({ id: 'game-1', title: 'Brass Birmingham' }),
    },
  },
}));

// ─── Wrapper ──────────────────────────────────────────────────────────────────

function createWrapper(queryClient: QueryClient) {
  return ({ children }: { children: ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('useGameNightPrefill', () => {
  it('maps gamenight + accepted rsvps + game into form initial values', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useGameNightPrefill('gn-1'), {
      wrapper: createWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.prefill).not.toBeNull());

    expect(result.current.prefill!.initialValues).toMatchObject({
      gameType: 'catalog',
      gameId: 'game-1',
      gameName: 'Brass Birmingham',
      location: 'Padova',
    });

    // Only the Accepted RSVP (Marco) should be mapped — Davide (Declined) is excluded
    expect(result.current.prefill!.initialPlayers).toHaveLength(1);
    expect(result.current.prefill!.initialPlayers[0]).toMatchObject({ name: 'Marco' });
  });

  it('is inert when gameNightId is null', () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useGameNightPrefill(null), {
      wrapper: createWrapper(queryClient),
    });

    expect(result.current.prefill).toBeNull();
    expect(result.current.enabled).toBe(false);
  });
});
