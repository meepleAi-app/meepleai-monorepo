/**
 * Tests for color-utils.ts
 *
 * Issue #572: V2 Phase 0 — entityHsl() helper
 * 9 entity types × {no alpha, with alpha} = 18 cases for entityHsl.
 * Plus baseline coverage for the pre-existing hexToHsl helper.
 */
import { describe, expect, it } from 'vitest';

import { entityHsl, hexToHsl } from './color-utils';
import type { EntityType } from '@/components/ui/v2/entity-tokens';

const ENTITY_TYPES: readonly EntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
] as const;

describe('entityHsl', () => {
  describe.each(ENTITY_TYPES)('entity=%s', entity => {
    it('returns hsl(var(--c-<entity>)) when alpha is omitted', () => {
      expect(entityHsl(entity)).toBe(`hsl(var(--c-${entity}))`);
    });

    it('returns hsl(var(--c-<entity>) / <alpha>) when alpha is provided', () => {
      expect(entityHsl(entity, 0.1)).toBe(`hsl(var(--c-${entity}) / 0.1)`);
    });
  });

  it('emits explicit "/ 0" when alpha is exactly 0 (preserves caller intent)', () => {
    expect(entityHsl('game', 0)).toBe('hsl(var(--c-game) / 0)');
  });

  it('emits explicit "/ 1" when alpha is exactly 1 (preserves caller intent)', () => {
    expect(entityHsl('agent', 1)).toBe('hsl(var(--c-agent) / 1)');
  });
});

describe('hexToHsl (regression baseline)', () => {
  it('parses 6-digit hex with hash', () => {
    expect(hexToHsl('#7c3aed')).toBe('262 83% 58%');
  });

  it('parses 3-digit hex without hash', () => {
    expect(hexToHsl('fff')).toBe('0 0% 100%');
  });

  it('returns undefined for invalid input', () => {
    expect(hexToHsl('nope')).toBeUndefined();
  });
});
