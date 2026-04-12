import { describe, it, expect } from 'vitest';
import { buildGameConnections, buildSessionConnections } from '../build-connections';

describe('buildGameConnections', () => {
  it('returns 4 pips: agent, kb, chat, session', () => {
    const pips = buildGameConnections({
      agentCount: 1,
      kbCount: 3,
      chatCount: 5,
      sessionCount: 23,
    });
    expect(pips).toHaveLength(4);
    expect(pips.map(p => p.entityType)).toEqual(['agent', 'kb', 'chat', 'session']);
  });

  it('sets count correctly', () => {
    const pips = buildGameConnections({ agentCount: 0, kbCount: 2, chatCount: 0, sessionCount: 1 });
    expect(pips[0].count).toBe(0); // agent
    expect(pips[1].count).toBe(2); // kb
  });

  it('sets isEmpty = true when count is 0', () => {
    const pips = buildGameConnections({ agentCount: 0, kbCount: 2, chatCount: 0, sessionCount: 1 });
    expect(pips[0].isEmpty).toBe(true); // agent count 0
    expect(pips[1].isEmpty).toBe(false); // kb count 2
    expect(pips[2].isEmpty).toBe(true); // chat count 0
    expect(pips[3].isEmpty).toBe(false); // session count 1
  });
});

describe('buildSessionConnections', () => {
  it('returns 4 pips: game, player, tool, agent', () => {
    const pips = buildSessionConnections({
      gameCount: 1,
      playerCount: 4,
      toolCount: 3,
      agentCount: 1,
    });
    expect(pips).toHaveLength(4);
    expect(pips.map(p => p.entityType)).toEqual(['game', 'player', 'tool', 'agent']);
  });

  it('sets isEmpty correctly', () => {
    const pips = buildSessionConnections({
      gameCount: 0,
      playerCount: 4,
      toolCount: 0,
      agentCount: 1,
    });
    expect(pips[0].isEmpty).toBe(true); // game count 0
    expect(pips[1].isEmpty).toBe(false); // player count 4
    expect(pips[2].isEmpty).toBe(true); // tool count 0
    expect(pips[3].isEmpty).toBe(false); // agent count 1
  });
});
