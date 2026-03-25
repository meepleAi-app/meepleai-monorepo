import type { MeepleEntityType } from '../meeple-card-styles';

export type ManaSize = 'full' | 'medium' | 'small' | 'mini';

export interface ManaDisplayConfig {
  key: MeepleEntityType;
  displayName: string;
  symbol: string;
  tier: 'core' | 'social' | 'ai' | 'tools';
}

export type EntityRelationshipMap = Record<MeepleEntityType, MeepleEntityType[]>;
