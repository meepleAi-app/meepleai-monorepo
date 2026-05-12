/**
 * Tests for WS-D Foundation parser (refs #1070, umbrella #1066).
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 */
import { describe, it, expect } from 'vitest';
import { extractStatesFromComment, extractRouteFromComment } from '../extract-mockup-states';

describe('extractStatesFromComment', () => {
  it('parses single-line Stati with · separator', () => {
    const comment = `
      Mockup: nanolith-runthrough-game-detail
      Route:  /library/{gameId}
      Stati:  default · loading · error · not-found
      Persona: Aaron`;
    expect(extractStatesFromComment(comment)).toEqual(['default', 'loading', 'error', 'not-found']);
  });

  it('parses multi-line Stati with indented continuation', () => {
    const comment = `
      Mockup: sp4-game-chat-tab
      Stati:
        default            (Aaron query "azione carta uccello + 2 cibo" su Wingspan,
                           risposta IT + citation Manuale p.12 + confidence alta verde)
        confidence-bassa   (edge "mazzo finisce a metà partita", disclaimer
                           "non sono certo" + suggerimento BGG)
        out-of-context     (utente chiede regole di Tainted Grail mentre è su Wingspan,
                           agente declina + propone switch gioco/agente)
        loading            (typing indicator + meta latency live, budget P95 ≤ 10s da Q6)
      Persona: Aaron`;
    expect(extractStatesFromComment(comment)).toEqual([
      'default',
      'confidence-bassa',
      'out-of-context',
      'loading',
    ]);
  });

  it('returns empty array when no Stati line present', () => {
    const comment = `Mockup: foo\nRoute: /bar`;
    expect(extractStatesFromComment(comment)).toEqual([]);
  });
});

describe('extractRouteFromComment', () => {
  it('extracts Route: from comment', () => {
    const comment = `
      Mockup: foo
      Route:  /library/{gameId}
      Stati: default`;
    expect(extractRouteFromComment(comment)).toBe('/library/{gameId}');
  });

  it('returns null when no Route declared', () => {
    const comment = `Mockup: foo\nStati: default`;
    expect(extractRouteFromComment(comment)).toBeNull();
  });
});
