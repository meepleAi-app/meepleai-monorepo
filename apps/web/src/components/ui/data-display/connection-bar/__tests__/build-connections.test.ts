import { describe, it, expect } from 'vitest';

import {
  buildGameConnectionPips,
  buildPlayerConnectionPips,
  buildSessionConnectionPips,
  buildAgentConnectionPips,
  buildKbConnectionPips,
  buildChatConnectionPips,
  buildEventConnectionPips,
  buildToolkitConnectionPips,
  buildToolConnectionPips,
} from '../build-connections';

// Locks each builder's slot order (entityType sequence) against silent drift (#2860).
describe('connection-bar build*ConnectionPips slot order', () => {
  it('game -> agent, kb, chat, session', () => {
    const pips = buildGameConnectionPips({
      agentCount: 1,
      kbCount: 3,
      chatCount: 5,
      sessionCount: 2,
    });
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'kb', 'chat', 'session']);
  });
  it('player -> session, game', () => {
    const pips = buildPlayerConnectionPips({ sessionCount: 4, favoriteGameCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['session', 'game']);
  });
  it('session -> game, player, tool, agent', () => {
    const pips = buildSessionConnectionPips({
      gameCount: 1,
      playerCount: 4,
      toolCount: 3,
      agentCount: 1,
    });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'player', 'tool', 'agent']);
  });
  it('agent -> game, kb, chat', () => {
    const pips = buildAgentConnectionPips({ gameCount: 1, kbCount: 2, chatCount: 3 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'kb', 'chat']);
  });
  it('kb -> game, agent', () => {
    const pips = buildKbConnectionPips({ gameCount: 1, agentCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'agent']);
  });
  it('chat -> agent, game', () => {
    const pips = buildChatConnectionPips({ agentCount: 1, gameCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'game']);
  });
  it('event -> player, game, session', () => {
    const pips = buildEventConnectionPips({ participantCount: 5, gameCount: 2, sessionCount: 1 });
    expect(pips.map(p => p.entityType)).toEqual(['player', 'game', 'session']);
  });
  it('toolkit -> game, tool, session', () => {
    const pips = buildToolkitConnectionPips({ gameCount: 1, toolCount: 4, sessionCount: 2 });
    expect(pips.map(p => p.entityType)).toEqual(['game', 'tool', 'session']);
  });
  it('tool -> toolkit', () => {
    const pips = buildToolConnectionPips({ toolkitCount: 3 });
    expect(pips.map(p => p.entityType)).toEqual(['toolkit']);
  });
  it('sets isEmpty when count is 0', () => {
    const pips = buildGameConnectionPips({
      agentCount: 0,
      kbCount: 2,
      chatCount: 0,
      sessionCount: 1,
    });
    expect(pips.map(p => p.isEmpty)).toEqual([true, false, true, false]);
  });
});
