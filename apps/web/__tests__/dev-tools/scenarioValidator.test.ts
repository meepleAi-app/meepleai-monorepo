import { describe, it, expect } from 'vitest';
import { validateScenario, SCENARIO_FALLBACK } from '@/dev-tools/scenarioValidator';
import type { Scenario } from '@/dev-tools/types';

const validScenario: Scenario = {
  name: 'test',
  description: 'Test scenario',
  auth: {
    currentUser: {
      id: 'MOCK-11111111-1111-1111-1111-111111111111',
      email: 'test@meeple.local',
      displayName: 'Test',
      role: 'User',
    },
    availableUsers: [],
  },
  games: [],
  sessions: [],
  library: { ownedGameIds: [], wishlistGameIds: [] },
  chatHistory: [],
};

describe('scenarioValidator', () => {
  it('validates a correct scenario', () => {
    const result = validateScenario(validScenario);
    expect(result.valid).toBe(true);
    expect(result.errors).toEqual([]);
  });

  it('rejects a scenario missing required fields', () => {
    const invalid = { name: 'bad' };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
    expect(result.errors.length).toBeGreaterThan(0);
  });

  it('rejects a scenario with invalid user role', () => {
    const invalid = {
      ...validScenario,
      auth: {
        ...validScenario.auth,
        currentUser: { ...validScenario.auth.currentUser, role: 'Wizard' },
      },
    };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
  });

  it('rejects user id not starting with MOCK-', () => {
    const invalid = {
      ...validScenario,
      auth: {
        ...validScenario.auth,
        currentUser: { ...validScenario.auth.currentUser, id: 'real-uuid' },
      },
    };
    const result = validateScenario(invalid);
    expect(result.valid).toBe(false);
  });

  it('provides a fallback scenario', () => {
    expect(SCENARIO_FALLBACK.name).toBe('empty');
    expect(SCENARIO_FALLBACK.auth.currentUser.role).toBe('Guest');
  });
});
