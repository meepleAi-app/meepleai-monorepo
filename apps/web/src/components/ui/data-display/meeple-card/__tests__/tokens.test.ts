import { describe, it, expect } from 'vitest';
import { entityColors, entityHsl, entityLabel, entityIcon } from '../tokens';
import type { MeepleEntityType } from '../types';

const allEntities: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];

describe('entityColors', () => {
  it.each(allEntities)('has color definition for %s', (entity) => {
    const color = entityColors[entity];
    expect(color).toBeDefined();
    expect(color.h).toBeTypeOf('number');
    expect(color.s).toMatch(/%$/);
    expect(color.l).toMatch(/%$/);
  });

  it('has exactly 9 entity types', () => {
    expect(Object.keys(entityColors)).toHaveLength(9);
  });
});

describe('entityHsl', () => {
  it('returns hsl string without alpha', () => {
    expect(entityHsl('game')).toMatch(/^hsl\(/);
  });

  it('returns hsla string with alpha', () => {
    expect(entityHsl('game', 0.5)).toMatch(/^hsla\(/);
  });
});

describe('entityLabel', () => {
  it.each(allEntities)('has label for %s', (entity) => {
    expect(entityLabel[entity]).toBeTypeOf('string');
    expect(entityLabel[entity].length).toBeGreaterThan(0);
  });
});

describe('entityIcon', () => {
  it.each(allEntities)('has icon for %s', (entity) => {
    expect(entityIcon[entity]).toBeTypeOf('string');
  });
});
