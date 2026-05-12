/**
 * Tests for WS-D Foundation parser (refs #1070, umbrella #1066).
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 */
import { describe, it, expect } from 'vitest';
import { extractStatesFromComment } from '../extract-mockup-states';

describe('extractStatesFromComment', () => {
  it('parses single-line Stati with · separator', () => {
    const comment = `
      Mockup: nanolith-runthrough-game-detail
      Route:  /library/{gameId}
      Stati:  default · loading · error · not-found
      Persona: Aaron`;
    expect(extractStatesFromComment(comment)).toEqual(['default', 'loading', 'error', 'not-found']);
  });
});
