/**
 * ExtraMeepleCard - Barrel exports
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 * Issue #4762 - Media Tab + AI Tab + Other Entity Types
 */

export { ExtraMeepleCard } from './ExtraMeepleCard';
export {
  GameExtraMeepleCard,
  PlayerExtraMeepleCard,
  CollectionExtraMeepleCard,
} from './EntityExtraMeepleCard';
export { MediaTab } from './tabs/MediaTab';
export { AITab } from './tabs/AITab';
export type {
  ExtraMeepleCardProps,
  ExtraMeepleCardTab,
  ExtraMeepleCardEntity,
  TabConfig,
  OverviewTabData,
  ScoreboardTabData,
  HistoryTabData,
  ToolkitData,
  SnapshotInfo,
  DiceToolInfo,
  CardToolInfo,
  TimerToolInfo,
  CounterToolInfo,
  ScoringTemplateInfo,
  TurnTemplateInfo,
  MediaItem,
  MediaTabData,
  AIChatMessage,
  AISource,
  AIQuickAction,
  AITabData,
  GameDetailData,
  PlayerDetailData,
  CollectionDetailData,
} from './types';
