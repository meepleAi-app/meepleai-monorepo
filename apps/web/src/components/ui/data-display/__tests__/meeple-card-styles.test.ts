/**
 * MeepleCardStyles Unit Tests
 *
 * Verifies all 16 entity types exist in entityColors with valid HSL values and unique colors.
 * Issue #Mana: Extended MeepleEntityType from 10 to 16 types.
 */

import { describe, it, expect } from 'vitest';
import { entityColors, type MeepleEntityType } from '../meeple-card-styles';

const EXPECTED_ENTITY_TYPES: MeepleEntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chatSession',
  'event',
  'toolkit',
  'tool',
  'custom',
  'collection',
  'group',
  'location',
  'expansion',
  'achievement',
  'note',
];

const HSL_PATTERN = /^\d{1,3} \d{1,3}% \d{1,3}%$/;

describe('entityColors', () => {
  it('contains all 16 entity types', () => {
    expect(Object.keys(entityColors)).toHaveLength(16);

    for (const type of EXPECTED_ENTITY_TYPES) {
      expect(entityColors).toHaveProperty(type);
    }
  });

  it('each entity has a valid HSL string', () => {
    for (const type of EXPECTED_ENTITY_TYPES) {
      const color = entityColors[type];
      expect(color, `${type} must have a color entry`).toBeDefined();
      expect(color.hsl, `${type}.hsl must match HSL pattern`).toMatch(HSL_PATTERN);
      expect(color.name, `${type}.name must be a non-empty string`).toBeTruthy();
    }
  });

  it('all HSL values are unique', () => {
    const hslValues = EXPECTED_ENTITY_TYPES.map(t => entityColors[t].hsl);
    const uniqueValues = new Set(hslValues);
    expect(uniqueValues.size).toBe(hslValues.length);
  });

  it('custom color is Silver (220 15% 45%)', () => {
    expect(entityColors.custom.hsl).toBe('220 15% 45%');
  });

  it('new entity types have correct colors', () => {
    expect(entityColors.collection.hsl).toBe('20 70% 42%');
    expect(entityColors.collection.name).toBe('Copper');

    expect(entityColors.group.hsl).toBe('280 50% 48%');
    expect(entityColors.group.name).toBe('Warm Violet');

    expect(entityColors.location.hsl).toBe('200 55% 45%');
    expect(entityColors.location.name).toBe('Slate Cyan');

    expect(entityColors.expansion.hsl).toBe('290 65% 50%');
    expect(entityColors.expansion.name).toBe('Magenta');

    expect(entityColors.achievement.hsl).toBe('45 90% 48%');
    expect(entityColors.achievement.name).toBe('Gold');

    expect(entityColors.note.hsl).toBe('40 30% 42%');
    expect(entityColors.note.name).toBe('Warm Gray');
  });
});
