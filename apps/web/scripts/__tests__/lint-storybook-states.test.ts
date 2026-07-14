/**
 * lint-storybook-states.test.ts — unit tests for lint-storybook-states.mjs
 * (DEC-A5 canonical-state coverage gate, umbrella #2342)
 *
 * Run: pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts
 */
import { describe, it, expect } from 'vitest';
import { CANONICAL_STATES, normalizeState, detectStates } from '../lint-storybook-states.mjs';

describe('CANONICAL_STATES', () => {
  it('is exactly the 5 DEC-A5 states in order', () => {
    expect([...CANONICAL_STATES]).toEqual(['default', 'empty', 'loading', 'error', 'sse']);
  });
});

describe('normalizeState', () => {
  it('passes the 5 canonical states through unchanged', () => {
    for (const s of ['default', 'empty', 'loading', 'error', 'sse']) {
      expect(normalizeState(s)).toBe(s);
    }
  });
  it('collapses empty-* variants to empty', () => {
    expect(normalizeState('empty-first-run')).toBe('empty');
    expect(normalizeState('empty-filtered')).toBe('empty');
    expect(normalizeState('empty-tab-agents')).toBe('empty');
  });
  it('discards non-canonical states', () => {
    expect(normalizeState('offline')).toBeNull();
    expect(normalizeState('quota-soft')).toBeNull();
    expect(normalizeState('quota-hard')).toBeNull();
    expect(normalizeState('segmenting')).toBeNull();
  });
  it('is case-insensitive and trims, discards non-strings', () => {
    expect(normalizeState('  Loading ')).toBe('loading');
    expect(normalizeState(42 as unknown as string)).toBeNull();
    expect(normalizeState('')).toBeNull();
  });
});

describe('detectStates — heuristic', () => {
  it('picks up mswForState() calls and normalizes empty variants', () => {
    const src = `
      export const A = { parameters: { msw: { handlers: mswForState('default') } } };
      export const B = { parameters: { msw: { handlers: mswForState('loading') } } };
      export const C = { parameters: { msw: { handlers: mswForState('error') } } };
      const options = ['default','empty-first-run','loading','error'];
    `;
    expect(detectStates(src)).toEqual(new Set(['default', 'empty', 'loading', 'error']));
  });
  it('does not treat a non-state string literal as a state', () => {
    const src = `HttpResponse.json({ error: 'server error' }, { status: 500 })`;
    expect(detectStates(src)).toEqual(new Set());
  });
});

describe('detectStates — explicit override wins', () => {
  it('uses parameters.canonicalStates verbatim, ignoring heuristic', () => {
    const src = `
      const meta = { parameters: { canonicalStates: ['default','loading','sse'] } };
      // no mswForState here, only phase names:
      const phases = ['idle','segmenting','translating'];
    `;
    expect(detectStates(src)).toEqual(new Set(['default', 'loading', 'sse']));
  });
});
