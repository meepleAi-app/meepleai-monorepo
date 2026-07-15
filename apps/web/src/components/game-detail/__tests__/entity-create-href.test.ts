import { describe, it, expect } from 'vitest';

import { getEntityCreateHref } from '../entity-create-href';

describe('getEntityCreateHref', () => {
  const gameId = 'game-abc-123';

  it('maps agent to the game-scoped agent creation route', () => {
    expect(getEntityCreateHref('agent', gameId)).toBe(`/library/${gameId}/agent`);
  });

  it('maps kb to the game-scoped KB creation route', () => {
    expect(getEntityCreateHref('kb', gameId)).toBe(`/library/${gameId}/kb`);
  });

  it('maps chat to the new-chat wizard prefilled with the game (?game=)', () => {
    expect(getEntityCreateHref('chat', gameId)).toBe(`/chat/new?game=${gameId}`);
  });

  it('maps session to the new-session wizard prefilled with the game (?gameId=)', () => {
    expect(getEntityCreateHref('session', gameId)).toBe(`/sessions/new?gameId=${gameId}`);
  });

  it('returns null for entity types without a game-scoped create surface', () => {
    expect(getEntityCreateHref('player', gameId)).toBeNull();
    expect(getEntityCreateHref('game', gameId)).toBeNull();
    expect(getEntityCreateHref('toolkit', gameId)).toBeNull();
  });
});
