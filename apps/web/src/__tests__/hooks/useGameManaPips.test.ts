import { describe, it, expect } from 'vitest';

import {
  buildGameManaPips,
  type GameManaPipsData,
  type GameManaPipsActions,
} from '@/hooks/queries/useGameManaPips';

function makeData(overrides?: Partial<GameManaPipsData>): GameManaPipsData {
  return {
    sessions: { count: 0, items: [] },
    kbs: { count: 0, items: [], indexedCount: 0, processingCount: 0 },
    agents: { count: 0, items: [] },
    ...overrides,
  };
}

describe('buildGameManaPips', () => {
  it('returns empty array when data is undefined', () => {
    expect(buildGameManaPips(undefined)).toEqual([]);
  });

  it('returns three pips for session, kb, agent', () => {
    const pips = buildGameManaPips(makeData());
    expect(pips).toHaveLength(3);
    expect(pips.map(p => p.entityType)).toEqual(['session', 'kb', 'agent']);
  });

  it('passes counts and items through', () => {
    const data = makeData({
      sessions: {
        count: 2,
        items: [
          { id: '1', label: 'Session 1', href: '/sessions/1' },
          { id: '2', label: 'Session 2', href: '/sessions/2' },
        ],
      },
    });
    const pips = buildGameManaPips(data);
    const sessionPip = pips.find(p => p.entityType === 'session')!;
    expect(sessionPip.count).toBe(2);
    expect(sessionPip.items).toHaveLength(2);
  });

  it('attaches actions when provided', () => {
    const actions: GameManaPipsActions = {
      onCreateSession: () => {},
      onCreateKb: () => {},
      onCreateAgent: () => {},
    };
    const pips = buildGameManaPips(makeData(), actions);
    expect(pips[0].onCreate).toBe(actions.onCreateSession);
    expect(pips[0].createLabel).toBe('Nuova sessione');
    expect(pips[1].onCreate).toBe(actions.onCreateKb);
    expect(pips[1].createLabel).toBe('Carica PDF');
    expect(pips[2].onCreate).toBe(actions.onCreateAgent);
    expect(pips[2].createLabel).toBe('Crea agente');
  });

  // ========== KB Color Override Tests ==========

  it('sets green colorOverride on KB pip when indexedCount > 0', () => {
    const data = makeData({
      kbs: { count: 3, items: [], indexedCount: 2, processingCount: 1 },
    });
    const pips = buildGameManaPips(data);
    const kbPip = pips.find(p => p.entityType === 'kb')!;
    expect(kbPip.colorOverride).toBe('hsl(142, 71%, 45%)');
  });

  it('sets yellow colorOverride on KB pip when only processingCount > 0', () => {
    const data = makeData({
      kbs: { count: 2, items: [], indexedCount: 0, processingCount: 2 },
    });
    const pips = buildGameManaPips(data);
    const kbPip = pips.find(p => p.entityType === 'kb')!;
    expect(kbPip.colorOverride).toBe('hsl(45, 93%, 47%)');
  });

  it('sets grey colorOverride on KB pip when no docs indexed or processing', () => {
    const data = makeData({
      kbs: { count: 0, items: [], indexedCount: 0, processingCount: 0 },
    });
    const pips = buildGameManaPips(data);
    const kbPip = pips.find(p => p.entityType === 'kb')!;
    expect(kbPip.colorOverride).toBe('hsl(0, 0%, 60%)');
  });

  it('defaults to grey when indexedCount/processingCount are undefined', () => {
    const data = makeData({
      kbs: { count: 0, items: [] },
    });
    const pips = buildGameManaPips(data);
    const kbPip = pips.find(p => p.entityType === 'kb')!;
    expect(kbPip.colorOverride).toBe('hsl(0, 0%, 60%)');
  });

  it('does not set colorOverride on session or agent pips', () => {
    const pips = buildGameManaPips(makeData());
    const sessionPip = pips.find(p => p.entityType === 'session')!;
    const agentPip = pips.find(p => p.entityType === 'agent')!;
    expect(sessionPip.colorOverride).toBeUndefined();
    expect(agentPip.colorOverride).toBeUndefined();
  });
});
