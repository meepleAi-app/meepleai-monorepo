/**
 * derivePerspective — Task 0.1 (Issue #1488).
 *
 * Pure function that classifies a play record from the current user's POV:
 *   won | lost | tie | cooperative | spectator | pending
 *
 * Coverage matrix:
 *   1. won           — current user IS in winnerPlayerIds (single winner)
 *   2. lost          — current user is player BUT NOT winner
 *   3. tie           — 2+ winners include current user
 *   4. cooperative   — outcomeType === 'none' (EC-1)
 *   5. spectator(a)  — currentUserId NOT in players[].userId (EC-4)
 *   6. spectator(b)  — all players are guests, currentUser has no match (EC-5)
 *   7. pending       — status is InProgress or Planned (EC-6)
 */
import { describe, expect, it } from 'vitest';

import { derivePerspective } from '../derivePerspective';

describe('derivePerspective', () => {
  it('returns "won" when currentUser is in winnerPlayerIds', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [
        { id: 'p-1', userId: 'u-1', displayName: 'Me' },
        { id: 'p-2', userId: 'u-2', displayName: 'Other' },
      ],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'won', currentUserPlayerId: 'p-1' });
  });

  it('returns "lost" when currentUser is player but NOT winner', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [
        { id: 'p-1', userId: 'u-1', displayName: 'Me' },
        { id: 'p-2', userId: 'u-2', displayName: 'Other' },
      ],
      winnerPlayerIds: ['p-2'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'lost', currentUserPlayerId: 'p-1' });
  });

  it('returns "tie" when 2+ winners include currentUser', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [
        { id: 'p-1', userId: 'u-1', displayName: 'Me' },
        { id: 'p-2', userId: 'u-2', displayName: 'Other' },
      ],
      winnerPlayerIds: ['p-1', 'p-2'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'tie', currentUserPlayerId: 'p-1' });
  });

  it('returns "cooperative" when outcomeType is none (EC-1)', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Me' }],
      winnerPlayerIds: [],
      outcomeType: 'none',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'cooperative', currentUserPlayerId: 'p-1' });
  });

  it('returns "spectator" when currentUser NOT in players (EC-4)', () => {
    const result = derivePerspective({
      currentUserId: 'u-99',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Other' }],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'spectator', currentUserPlayerId: null });
  });

  it('returns "spectator" when all players are guests (EC-5)', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: null, displayName: 'Guest' }],
      winnerPlayerIds: ['p-1'],
      outcomeType: 'competitive',
      status: 'Completed',
    });
    expect(result).toEqual({ kind: 'spectator', currentUserPlayerId: null });
  });

  it('returns "pending" for InProgress/Planned', () => {
    const result = derivePerspective({
      currentUserId: 'u-1',
      players: [{ id: 'p-1', userId: 'u-1', displayName: 'Me' }],
      winnerPlayerIds: [],
      outcomeType: 'competitive',
      status: 'InProgress',
    });
    expect(result).toEqual({ kind: 'pending', currentUserPlayerId: 'p-1' });
  });
});
