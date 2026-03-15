import type { MeepleEntityType } from '../meeple-card-styles';

export type BlockType =
  | 'stats'
  | 'actions'
  | 'timeline'
  | 'ranking'
  | 'kbPreview'
  | 'members'
  | 'contents'
  | 'history'
  | 'progress'
  | 'notes'
  | 'detailLink';

export interface BlockAction {
  label: string;
  icon?: string;
  onClick: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
}

export type BlockData =
  | { type: 'stats'; entries: Array<{ label: string; value: string | number; icon?: string }> }
  | { type: 'actions'; actions: BlockAction[] }
  | { type: 'timeline'; events: Array<{ time: string; label: string; icon?: string }> }
  | {
      type: 'ranking';
      players: Array<{ name: string; score: number; position: number; isLeader?: boolean }>;
    }
  | {
      type: 'kbPreview';
      docsCount: number;
      chunksCount: number;
      lastQuery?: string;
      indexStatus: string;
    }
  | { type: 'members'; members: Array<{ name: string; role?: string; avatarUrl?: string }> }
  | {
      type: 'contents';
      items: Array<{ title: string; entityType: MeepleEntityType; id: string; status?: string }>;
    }
  | { type: 'history'; entries: Array<{ timestamp: string; message: string; sender?: string }> }
  | {
      type: 'progress';
      current: number;
      target: number;
      label: string;
      milestones?: Array<{ at: number; label: string }>;
    }
  | { type: 'notes'; content: string; updatedAt: string }
  | { type: 'detailLink'; href: string; label: string };

export interface CardBackBlock {
  type: BlockType;
  title: string;
  entityColor: string; // HSL format: "25 95% 45%"
  data: BlockData;
  actions?: BlockAction[];
}

export interface BlockConfig {
  type: BlockType;
  title: string;
}
