import { getBlocksForEntity } from '../block-registry';

describe('block-registry', () => {
  it('returns correct blocks for game entity', () => {
    const blocks = getBlocksForEntity('game');
    expect(blocks.map(b => b.type)).toEqual(['stats', 'actions', 'kbPreview']);
  });

  it('returns correct blocks for session entity', () => {
    const blocks = getBlocksForEntity('session');
    expect(blocks.map(b => b.type)).toEqual(['ranking', 'timeline', 'actions']);
  });

  it('returns correct blocks for player entity', () => {
    const blocks = getBlocksForEntity('player');
    expect(blocks.map(b => b.type)).toEqual(['stats', 'actions', 'progress']);
  });

  it('returns blocks for all 16 entity types', () => {
    const allTypes = [
      'game',
      'session',
      'player',
      'event',
      'collection',
      'group',
      'location',
      'expansion',
      'agent',
      'kb',
      'chatSession',
      'note',
      'toolkit',
      'tool',
      'achievement',
      'custom',
    ] as const;
    allTypes.forEach(type => {
      const blocks = getBlocksForEntity(type);
      expect(blocks.length).toBeGreaterThan(0);
    });
  });

  it('every entity has at least an actions block', () => {
    const allTypes = [
      'game',
      'session',
      'player',
      'event',
      'collection',
      'group',
      'location',
      'expansion',
      'agent',
      'kb',
      'chatSession',
      'note',
      'toolkit',
      'tool',
      'achievement',
      'custom',
    ] as const;
    allTypes.forEach(type => {
      const blocks = getBlocksForEntity(type);
      expect(blocks.some(b => b.type === 'actions')).toBe(true);
    });
  });
});
