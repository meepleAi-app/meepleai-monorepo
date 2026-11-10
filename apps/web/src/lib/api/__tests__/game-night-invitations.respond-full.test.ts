/**
 * respondToInvitation on a 409 — #4947.
 *
 * Two different 409s come back from the respond endpoint: «you already answered the other way» (D2 b, a state
 * switch) and «the table is full» (`game_night_full`). Before #4947 every 409 was read as the first, so a guest who
 * had never answered was told they had. The body's `error` code tells them apart.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../core/httpClient', () => ({
  getApiBase: () => 'https://api.test',
}));

import { GAME_NIGHT_FULL_CODE, respondToInvitation } from '../game-night-invitations';

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function conflict(body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), {
    status: 409,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('respondToInvitation — the two 409s', () => {
  it('reads a full table as `full`, not as a state switch', async () => {
    fetchMock.mockResolvedValue(
      conflict({ error: GAME_NIGHT_FULL_CODE, message: 'This game night is full' })
    );

    const result = await respondToInvitation('tok-abc', 'Accepted');

    expect(result).toEqual({ kind: 'full' });
  });

  it('still reads a plain conflict as the state switch it is', async () => {
    fetchMock.mockResolvedValue(
      conflict({
        error: 'conflict',
        message: 'Cannot change response: invitation is already Declined.',
      })
    );

    const result = await respondToInvitation('tok-abc', 'Accepted');

    expect(result).toEqual({
      kind: 'conflict-state-switch',
      currentlyRespondedAs: 'Declined',
      attemptedAction: 'Accepted',
    });
  });

  it('a 409 with no readable body is still the state switch, as before', async () => {
    fetchMock.mockResolvedValue(new Response('not json', { status: 409 }));

    const result = await respondToInvitation('tok-abc', 'Declined');

    expect(result.kind).toBe('conflict-state-switch');
  });

  it('names the code the backend uses, so a drift there is caught here', () => {
    expect(GAME_NIGHT_FULL_CODE).toBe('game_night_full');
  });
});
