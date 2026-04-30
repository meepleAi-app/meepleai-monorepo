import { describe, expect, it } from 'vitest';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import { deriveStatus, type AgentDerivedStatus } from '../derive-status';

const baseAgent: Pick<AgentDto, 'isActive' | 'invocationCount'> = {
  isActive: true,
  invocationCount: 0,
};

describe('deriveStatus', () => {
  it("returns 'attivo' when isActive=true AND invocationCount > 0", () => {
    expect(deriveStatus({ isActive: true, invocationCount: 1 })).toBe('attivo');
    expect(deriveStatus({ isActive: true, invocationCount: 42 })).toBe('attivo');
  });

  it("returns 'in-setup' when isActive=true AND invocationCount === 0", () => {
    expect(deriveStatus({ isActive: true, invocationCount: 0 })).toBe('in-setup');
  });

  it("returns 'archiviato' when isActive=false (invocationCount=0)", () => {
    expect(deriveStatus({ isActive: false, invocationCount: 0 })).toBe('archiviato');
  });

  it("returns 'archiviato' when isActive=false (invocationCount>0)", () => {
    expect(deriveStatus({ isActive: false, invocationCount: 100 })).toBe('archiviato');
  });

  it('isIdle is not consulted (still attivo when active+used)', () => {
    const agent: Pick<AgentDto, 'isActive' | 'invocationCount' | 'isIdle'> = {
      isActive: true,
      invocationCount: 5,
      isIdle: true,
    };
    expect(deriveStatus(agent)).toBe('attivo');
  });

  it('boundary invocationCount=1 transitions in-setup → attivo', () => {
    expect(deriveStatus({ isActive: true, invocationCount: 0 })).toBe('in-setup');
    expect(deriveStatus({ isActive: true, invocationCount: 1 })).toBe('attivo');
  });

  it('large invocationCount stays attivo', () => {
    expect(deriveStatus({ isActive: true, invocationCount: 9999 })).toBe('attivo');
  });

  it('full status matrix combinations', () => {
    type MatrixCase = {
      isActive: boolean;
      invocationCount: number;
      expected: AgentDerivedStatus;
    };
    const cases: MatrixCase[] = [
      { isActive: true, invocationCount: 0, expected: 'in-setup' },
      { isActive: true, invocationCount: 1, expected: 'attivo' },
      { isActive: true, invocationCount: 100, expected: 'attivo' },
      { isActive: false, invocationCount: 0, expected: 'archiviato' },
      { isActive: false, invocationCount: 1, expected: 'archiviato' },
      { isActive: false, invocationCount: 100, expected: 'archiviato' },
    ];
    for (const { isActive, invocationCount, expected } of cases) {
      expect(deriveStatus({ isActive, invocationCount })).toBe(expected);
    }
  });

  it('does not mutate input', () => {
    const input = { ...baseAgent };
    deriveStatus(input);
    expect(input).toEqual(baseAgent);
  });
});
