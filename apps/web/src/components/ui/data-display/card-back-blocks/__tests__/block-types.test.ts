import type { BlockType, BlockData, CardBackBlock } from '../block-types';

describe('block-types', () => {
  it('BlockType covers all 11 block types', () => {
    const allTypes: BlockType[] = [
      'stats',
      'actions',
      'timeline',
      'ranking',
      'kbPreview',
      'members',
      'contents',
      'history',
      'progress',
      'notes',
      'detailLink',
    ];
    expect(allTypes).toHaveLength(11);
  });

  it('BlockData discriminated union type-narrows correctly', () => {
    const statsData: BlockData = {
      type: 'stats',
      entries: [{ label: 'Plays', value: 12, icon: '🎲' }],
    };
    expect(statsData.type).toBe('stats');
    if (statsData.type === 'stats') {
      expect(statsData.entries[0].label).toBe('Plays');
    }
  });

  it('CardBackBlock holds a block config', () => {
    const block: CardBackBlock = {
      type: 'stats',
      title: 'Statistics',
      entityColor: '25 95% 45%',
      data: { type: 'stats', entries: [{ label: 'Games', value: 5 }] },
    };
    expect(block.type).toBe('stats');
    expect(block.title).toBe('Statistics');
  });
});
