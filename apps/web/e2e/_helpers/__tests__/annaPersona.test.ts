import { describe, it, expect } from 'vitest';
import { ANNA_PERSONA, buildAnnaInitialState } from '../annaPersona';

describe('annaPersona fixture (#1929 DEC-C-1)', () => {
  it('ANNA_PERSONA exposes deterministic canonical fields', () => {
    expect(ANNA_PERSONA.email).toBe('anna.host@meepleai.test');
    expect(ANNA_PERSONA.displayName).toBe('Anna Host');
    expect(ANNA_PERSONA.role).toBe('user');
    expect(ANNA_PERSONA.userId).toBe('00000000-0000-4000-8000-000000000001');
    expect(ANNA_PERSONA.onboardingCompleted).toBe(true);
  });

  it('buildAnnaInitialState("journey1") returns 1 GN Published + 2 player roster', () => {
    const state = buildAnnaInitialState('journey1');
    expect(state.gameNightCount).toBe(1);
    expect(state.gameNightStatus).toBe('Published');
    expect(state.playerRosterCount).toBe(2);
    expect(state.libraryGameCount).toBe(0);
    expect(state.sessionCount).toBe(0);
  });

  it('buildAnnaInitialState("journey2") returns 0 GN + 1 library game', () => {
    const state = buildAnnaInitialState('journey2');
    expect(state.gameNightCount).toBe(0);
    expect(state.libraryGameCount).toBe(1);
    expect(state.playerRosterCount).toBe(0);
    expect(state.sessionCount).toBe(0);
  });

  it('buildAnnaInitialState("journey3") returns 1 game + 15 completed sessions', () => {
    const state = buildAnnaInitialState('journey3');
    expect(state.libraryGameCount).toBe(1);
    expect(state.sessionCount).toBe(15);
    expect(state.sessionStatus).toBe('Completed');
  });

  it('buildAnnaInitialState rejects unknown journey id', () => {
    // @ts-expect-error invalid journey id
    expect(() => buildAnnaInitialState('journey99')).toThrow(/unknown journey/i);
  });
});
