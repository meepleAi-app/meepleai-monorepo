import { MANA_DISPLAY, ENTITY_RELATIONSHIPS, getManaDisplayName } from '../mana-config';

describe('MANA_DISPLAY', () => {
  it('has display config for all 16 entity types', () => {
    expect(Object.keys(MANA_DISPLAY)).toHaveLength(16);
  });

  it('each config has required fields', () => {
    Object.values(MANA_DISPLAY).forEach(config => {
      expect(config.displayName).toBeTruthy();
      expect(config.symbol).toBeTruthy();
      expect(['core', 'social', 'ai', 'tools']).toContain(config.tier);
    });
  });
});

describe('ENTITY_RELATIONSHIPS', () => {
  it('has relationship arrays for all 16 entity types', () => {
    expect(Object.keys(ENTITY_RELATIONSHIPS)).toHaveLength(16);
  });

  it('custom has empty relationships by default', () => {
    expect(ENTITY_RELATIONSHIPS.custom).toEqual([]);
  });

  it('game links to session, kb, agent, expansion, collection, note', () => {
    expect(ENTITY_RELATIONSHIPS.game).toEqual(
      expect.arrayContaining(['session', 'kb', 'agent', 'expansion', 'collection', 'note'])
    );
  });
});

describe('getManaDisplayName', () => {
  it('returns display name for entity type', () => {
    expect(getManaDisplayName('kb')).toBe('Knowledge');
    expect(getManaDisplayName('chatSession')).toBe('Chat');
    expect(getManaDisplayName('game')).toBe('Game');
  });
});
