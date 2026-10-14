/**
 * SessionDrawerSheet — #4156 (D4): a session row opens `/sessions/{liveSessionId}`, the page of
 * the LiveGameSession correlated to the GameSession; a session without one has no page to open.
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { SessionDrawerSheet } from '../SessionDrawerSheet';

const mockGetSessions = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: { games: { getSessions: mockGetSessions } },
}));

const SESSIONS = [
  {
    id: 'game-session-1',
    liveSessionId: 'live-1',
    gameId: 'game-1',
    status: 'InProgress',
    startedAt: '2026-04-20T19:00:00Z',
    completedAt: null,
    playerCount: 3,
    players: [],
    winnerName: null,
    notes: null,
    durationMinutes: 40,
  },
  {
    id: 'game-session-2',
    liveSessionId: null,
    gameId: 'game-1',
    status: 'Completed',
    startedAt: '2026-04-18T19:00:00Z',
    completedAt: '2026-04-18T21:00:00Z',
    playerCount: 2,
    players: [],
    winnerName: 'Alice',
    notes: null,
    durationMinutes: 120,
  },
];

const originalLocation = window.location;

describe('SessionDrawerSheet', () => {
  beforeEach(() => {
    mockGetSessions.mockResolvedValue(SESSIONS);
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: { href: '' },
    });
  });

  afterEach(() => {
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: originalLocation,
    });
  });

  function renderSheet(onOpenChange = vi.fn()) {
    renderWithQuery(
      <SessionDrawerSheet open onOpenChange={onOpenChange} gameId="game-1" gameTitle="Azul" />
    );
    return { onOpenChange };
  }

  it('opens the page of the correlated live session', async () => {
    const { onOpenChange } = renderSheet();
    const row = (await screen.findByText('In corso')).closest('button') as HTMLButtonElement;
    expect(row).toBeEnabled();
    fireEvent.click(row);
    expect(window.location.href).toBe('/sessions/live-1');
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('has no page to open for a session without a live session', async () => {
    const { onOpenChange } = renderSheet();
    const row = (await screen.findByText('Completata')).closest('button') as HTMLButtonElement;
    await waitFor(() => expect(row).toBeDisabled());
    fireEvent.click(row);
    expect(window.location.href).toBe('');
    expect(onOpenChange).not.toHaveBeenCalled();
  });
});
