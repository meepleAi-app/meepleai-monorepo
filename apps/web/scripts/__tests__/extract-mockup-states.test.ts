/**
 * Tests for WS-D Foundation parser (refs #1070, umbrella #1066).
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 */
import { describe, it, expect } from 'vitest';
import {
  extractStatesFromComment,
  extractRouteFromComment,
  parseMockupFile,
  mergeMatrix,
} from '../extract-mockup-states';
import type { StateMatrix } from '../extract-mockup-states';

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

describe('parseMockupFile', () => {
  it('extracts MockupStateEntry from HTML string', () => {
    const html = `<!doctype html>
<html>
<head><title>test</title></head>
<!--
  Mockup: test-mockup
  Route:  /test/{id}
  Stati:  default · loading · error
  Persona: Aaron
-->
<body></body>
</html>`;
    const entry = parseMockupFile('admin-mockups/design_files/test-mockup.html', html);
    expect(entry).toEqual({
      mockup_path: 'admin-mockups/design_files/test-mockup.html',
      route: '/test/{id}',
      declared_states: ['default', 'loading', 'error'],
      covered_states: [],
      missing: ['default', 'loading', 'error'],
      enforced: false,
    });
  });

  it('returns entry with empty declared_states when no Stati line', () => {
    const html = `<!--\n  Mockup: foo\n-->`;
    const entry = parseMockupFile('admin-mockups/design_files/foo.html', html);
    expect(entry.declared_states).toEqual([]);
    expect(entry.missing).toEqual([]);
  });
});

describe('mergeMatrix', () => {
  it('preserves covered_states and enforced from existing matrix', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 1,
      enforced_count: 1,
      entries: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          route: '/foo',
          declared_states: ['a', 'b'],
          covered_states: ['a'],
          missing: ['b'],
          enforced: true,
        },
      ],
    };
    const fresh = [
      {
        mockup_path: 'admin-mockups/design_files/foo.html',
        route: '/foo',
        declared_states: ['a', 'b', 'c'],
        covered_states: [],
        missing: ['a', 'b', 'c'],
        enforced: false,
      },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries[0].covered_states).toEqual(['a']);
    expect(merged.entries[0].enforced).toBe(true);
    expect(merged.entries[0].declared_states).toEqual(['a', 'b', 'c']);
    expect(merged.entries[0].missing).toEqual(['b', 'c']);
  });

  it('drops covered states no longer in declared', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 1,
      enforced_count: 0,
      entries: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          route: '/foo',
          declared_states: ['a', 'b'],
          covered_states: ['a', 'b', 'obsolete'],
          missing: [],
          enforced: false,
        },
      ],
    };
    const fresh = [
      {
        mockup_path: 'admin-mockups/design_files/foo.html',
        route: '/foo',
        declared_states: ['a'],
        covered_states: [],
        missing: ['a'],
        enforced: false,
      },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries[0].covered_states).toEqual(['a']);
  });

  it('sorts entries alphabetically by mockup_path', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 0,
      enforced_count: 0,
      entries: [],
    };
    const fresh = [
      {
        mockup_path: 'admin-mockups/design_files/zeta.html',
        route: null,
        declared_states: [],
        covered_states: [],
        missing: [],
        enforced: false,
      },
      {
        mockup_path: 'admin-mockups/design_files/alpha.html',
        route: null,
        declared_states: [],
        covered_states: [],
        missing: [],
        enforced: false,
      },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries.map(e => e.mockup_path)).toEqual([
      'admin-mockups/design_files/alpha.html',
      'admin-mockups/design_files/zeta.html',
    ]);
  });
});
