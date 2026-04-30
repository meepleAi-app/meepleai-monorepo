import { describe, expect, it } from 'vitest';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import { deriveStats, filterByStatus, matchQuery, sortAgents } from '../library-filters';

const NOW = '2026-04-30T10:00:00.000Z';

const createAgent = (overrides: Partial<AgentDto> = {}): AgentDto => ({
  id: '00000000-0000-4000-8000-000000000aaa',
  name: 'Default Agent',
  type: 'Strategist',
  strategyName: 'qa-tutor',
  strategyParameters: {},
  isActive: true,
  createdAt: NOW,
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: false,
  ...overrides,
});

const ATTIVO = createAgent({
  id: '00000000-0000-4000-8000-000000000001',
  name: 'Catan Coach',
  isActive: true,
  invocationCount: 12,
  lastInvokedAt: '2026-04-29T10:00:00.000Z',
});

const IN_SETUP = createAgent({
  id: '00000000-0000-4000-8000-000000000002',
  name: 'Wingspan Tutor',
  isActive: true,
  invocationCount: 0,
});

const ARCHIVIATO = createAgent({
  id: '00000000-0000-4000-8000-000000000003',
  name: 'Pandemic Helper',
  isActive: false,
  invocationCount: 50,
  lastInvokedAt: '2026-02-01T10:00:00.000Z',
});

describe('filterByStatus', () => {
  const agents = [ATTIVO, IN_SETUP, ARCHIVIATO];

  it("returns all agents when status is 'all'", () => {
    expect(filterByStatus(agents, 'all')).toHaveLength(3);
  });

  it("returns only 'attivo' agents", () => {
    const result = filterByStatus(agents, 'attivo');
    expect(result).toHaveLength(1);
    expect(result[0].id).toBe(ATTIVO.id);
  });

  it("returns only 'in-setup' agents", () => {
    const result = filterByStatus(agents, 'in-setup');
    expect(result).toHaveLength(1);
    expect(result[0].id).toBe(IN_SETUP.id);
  });

  it("returns only 'archiviato' agents", () => {
    const result = filterByStatus(agents, 'archiviato');
    expect(result).toHaveLength(1);
    expect(result[0].id).toBe(ARCHIVIATO.id);
  });

  it('returns empty array when no matches', () => {
    expect(filterByStatus([ATTIVO], 'archiviato')).toEqual([]);
  });

  it('does not mutate the input array', () => {
    const input = [ATTIVO, IN_SETUP, ARCHIVIATO];
    const before = input.slice();
    filterByStatus(input, 'attivo');
    expect(input).toEqual(before);
  });

  it("returns a new array instance even with status='all'", () => {
    const input = [ATTIVO];
    const result = filterByStatus(input, 'all');
    expect(result).not.toBe(input);
  });
});

describe('matchQuery', () => {
  const agent = createAgent({
    name: 'Catan Coach',
    type: 'Strategist',
    strategyName: 'qa-tutor',
  });

  it('returns true for empty query', () => {
    expect(matchQuery(agent, '')).toBe(true);
  });

  it('returns true for whitespace-only query', () => {
    expect(matchQuery(agent, '   ')).toBe(true);
  });

  it('matches by name (case-insensitive)', () => {
    expect(matchQuery(agent, 'catan')).toBe(true);
    expect(matchQuery(agent, 'COACH')).toBe(true);
    expect(matchQuery(agent, 'Catan Coach')).toBe(true);
  });

  it('matches by type (case-insensitive)', () => {
    expect(matchQuery(agent, 'strategist')).toBe(true);
    expect(matchQuery(agent, 'STRATEGIST')).toBe(true);
  });

  it('matches by strategyName (case-insensitive)', () => {
    expect(matchQuery(agent, 'qa-tutor')).toBe(true);
    expect(matchQuery(agent, 'TUTOR')).toBe(true);
  });

  it('returns false when no field matches', () => {
    expect(matchQuery(agent, 'wingspan')).toBe(false);
    expect(matchQuery(agent, 'rules-lawyer')).toBe(false);
  });

  it('trims query whitespace before matching', () => {
    expect(matchQuery(agent, '  catan  ')).toBe(true);
  });

  it('matches partial substrings', () => {
    expect(matchQuery(agent, 'cat')).toBe(true);
    expect(matchQuery(agent, 'tu')).toBe(true); // tutor
  });
});

describe('sortAgents', () => {
  it("'recent' sorts by lastInvokedAt DESC", () => {
    const recent = createAgent({
      id: '1',
      lastInvokedAt: '2026-04-30T10:00:00.000Z',
    });
    const older = createAgent({
      id: '2',
      lastInvokedAt: '2026-04-01T10:00:00.000Z',
    });
    const middle = createAgent({
      id: '3',
      lastInvokedAt: '2026-04-15T10:00:00.000Z',
    });
    const result = sortAgents([older, recent, middle], 'recent');
    expect(result.map(a => a.id)).toEqual(['1', '3', '2']);
  });

  it("'recent' falls back to createdAt when lastInvokedAt is null", () => {
    const newCreated = createAgent({
      id: 'new',
      lastInvokedAt: null,
      createdAt: '2026-04-29T10:00:00.000Z',
    });
    const oldCreated = createAgent({
      id: 'old',
      lastInvokedAt: null,
      createdAt: '2026-04-01T10:00:00.000Z',
    });
    const result = sortAgents([oldCreated, newCreated], 'recent');
    expect(result.map(a => a.id)).toEqual(['new', 'old']);
  });

  it("'recent' mixes lastInvokedAt and createdAt deterministically", () => {
    const usedRecently = createAgent({
      id: 'used-recent',
      lastInvokedAt: '2026-04-29T10:00:00.000Z',
      createdAt: '2026-01-01T10:00:00.000Z',
    });
    const newButUnused = createAgent({
      id: 'new-unused',
      lastInvokedAt: null,
      createdAt: '2026-04-30T10:00:00.000Z',
    });
    const result = sortAgents([usedRecently, newButUnused], 'recent');
    expect(result[0].id).toBe('new-unused');
    expect(result[1].id).toBe('used-recent');
  });

  it("'alpha' sorts by name ASC localeCompare", () => {
    const beta = createAgent({ id: 'b', name: 'Beta' });
    const alpha = createAgent({ id: 'a', name: 'Alpha' });
    const gamma = createAgent({ id: 'g', name: 'Gamma' });
    const result = sortAgents([gamma, beta, alpha], 'alpha');
    expect(result.map(a => a.name)).toEqual(['Alpha', 'Beta', 'Gamma']);
  });

  it("'alpha' is case-insensitive (sensitivity: base)", () => {
    const upper = createAgent({ id: '1', name: 'ZULU' });
    const lower = createAgent({ id: '2', name: 'alpha' });
    const result = sortAgents([upper, lower], 'alpha');
    expect(result.map(a => a.name)).toEqual(['alpha', 'ZULU']);
  });

  it("'used' sorts by invocationCount DESC", () => {
    const heavy = createAgent({ id: 'heavy', invocationCount: 100 });
    const light = createAgent({ id: 'light', invocationCount: 5 });
    const medium = createAgent({ id: 'medium', invocationCount: 50 });
    const result = sortAgents([light, heavy, medium], 'used');
    expect(result.map(a => a.id)).toEqual(['heavy', 'medium', 'light']);
  });

  it('does not mutate input array', () => {
    const input = [createAgent({ id: 'b', name: 'Beta' }), createAgent({ id: 'a', name: 'Alpha' })];
    const before = input.slice();
    sortAgents(input, 'alpha');
    expect(input).toEqual(before);
  });
});

describe('deriveStats', () => {
  it('counts each derived status bucket and sums invocations', () => {
    const agents = [
      ATTIVO, // attivo, 12
      createAgent({
        id: 'second-attivo',
        isActive: true,
        invocationCount: 8,
      }),
      IN_SETUP, // in-setup, 0
      ARCHIVIATO, // archiviato, 50 (still counted in totalInvocations)
    ];
    expect(deriveStats(agents)).toEqual({
      attivo: 2,
      inSetup: 1,
      archiviato: 1,
      totalInvocations: 70,
    });
  });

  it('returns zeros for empty input', () => {
    expect(deriveStats([])).toEqual({
      attivo: 0,
      inSetup: 0,
      archiviato: 0,
      totalInvocations: 0,
    });
  });
});
