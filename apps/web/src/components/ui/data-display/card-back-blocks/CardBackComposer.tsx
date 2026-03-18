import { memo } from 'react';

import { entityColors } from '../meeple-card-styles';
import { getBlocksForEntity } from './block-registry';
import { ActionsBlock } from './blocks/ActionsBlock';
import { ContentsBlock } from './blocks/ContentsBlock';
import { DetailLinkBlock } from './blocks/DetailLinkBlock';
import { HistoryBlock } from './blocks/HistoryBlock';
import { KBPreviewBlock } from './blocks/KBPreviewBlock';
import { MembersBlock } from './blocks/MembersBlock';
import { NotesBlock } from './blocks/NotesBlock';
import { ProgressBlock } from './blocks/ProgressBlock';
import { RankingBlock } from './blocks/RankingBlock';
import { StatsBlock } from './blocks/StatsBlock';
import { TimelineBlock } from './blocks/TimelineBlock';

import type { BlockData, BlockType } from './block-types';
import type { MeepleEntityType } from '../meeple-card-styles';

export interface CardBackComposerProps {
  entity: MeepleEntityType;
  blockData: Partial<{ [K in BlockType]: Extract<BlockData, { type: K }> }>;
  className?: string;
}

// Each block component expects a specific discriminated variant of BlockData,
// but the record maps all BlockTypes to one component type signature.

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- each block expects a different BlockData variant
type BlockComponent = React.ComponentType<{ title: string; entityColor: string; data: any }>;

const BLOCK_COMPONENTS: Record<BlockType, BlockComponent> = {
  stats: StatsBlock,
  actions: ActionsBlock,
  timeline: TimelineBlock,
  ranking: RankingBlock,
  kbPreview: KBPreviewBlock,
  members: MembersBlock,
  contents: ContentsBlock,
  history: HistoryBlock,
  progress: ProgressBlock,
  notes: NotesBlock,
  detailLink: DetailLinkBlock,
};

const EMPTY_DATA: { [K in BlockType]: Extract<BlockData, { type: K }> } = {
  stats: { type: 'stats', entries: [] },
  actions: { type: 'actions', actions: [] },
  timeline: { type: 'timeline', events: [] },
  ranking: { type: 'ranking', players: [] },
  kbPreview: { type: 'kbPreview', docsCount: 0, chunksCount: 0, indexStatus: 'unknown' },
  members: { type: 'members', members: [] },
  contents: { type: 'contents', items: [] },
  history: { type: 'history', entries: [] },
  progress: { type: 'progress', current: 0, target: 0, label: '' },
  notes: { type: 'notes', content: '', updatedAt: '' },
  detailLink: { type: 'detailLink', href: '#', label: '' },
};

export const CardBackComposer = memo(function CardBackComposer({
  entity,
  blockData,
  className,
}: CardBackComposerProps) {
  const blockConfigs = getBlocksForEntity(entity);
  // eslint-disable-next-line security/detect-object-injection
  const color = entityColors[entity].hsl;

  return (
    <div className={className}>
      {blockConfigs.map(config => {
        const Component = BLOCK_COMPONENTS[config.type];
        const data = blockData[config.type] ?? EMPTY_DATA[config.type];
        return <Component key={config.type} title={config.title} entityColor={color} data={data} />;
      })}
    </div>
  );
});
