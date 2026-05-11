import { describe, it, expect } from 'vitest';
import { ENTITY_TOKENS, getEntityToken, type EntityType } from './entity-tokens';

describe('entity-tokens', () => {
  it('provides 9 canonical entity types', () => {
    const types: EntityType[] = [
      'game',
      'player',
      'session',
      'agent',
      'kb',
      'chat',
      'event',
      'toolkit',
      'tool',
    ];
    types.forEach(t => {
      const token = getEntityToken(t);
      expect(token.bg).toContain('bg-entity-');
      expect(token.text).toContain('text-entity-');
      expect(token.emoji).toBeTruthy();
      expect(token.label).toBeTruthy();
    });
  });

  it('maps kb to document tailwind class', () => {
    expect(getEntityToken('kb').bg).toBe('bg-entity-document');
  });

  it('returns emoji for toolkit as 🧰', () => {
    expect(getEntityToken('toolkit').emoji).toBe('🧰');
  });
});
