/**
 * #4618: `PATCH /game-sessions/{id}/agent/config` rispondeva 204 e non salvava niente: la rotta è tolta.
 * `agentApi` non ha più un modo di chiamarla.
 */

import { describe, expect, it } from 'vitest';

import { agentApi } from '../api-client';

describe('agentApi', () => {
  it('has no updateConfig: the config route of a session was retired (#4618)', () => {
    expect(typeof (agentApi as unknown as Record<string, unknown>).updateConfig).toBe('undefined');
  });

  it('still has the calls that exist on the backend', () => {
    expect(typeof agentApi.updateTypology).toBe('function');
    expect(typeof agentApi.endSession).toBe('function');
  });
});
