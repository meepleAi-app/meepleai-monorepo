/**
 * ExtraMeepleCard - Barrel exports
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 * Issue #4762 - Media Tab + AI Tab + Other Entity Types
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI
 * Issue #5026 - AgentExtraMeepleCard (Epic #5023)
 */

export { ExtraMeepleCard } from './ExtraMeepleCard';
export {
  GameExtraMeepleCard,
  PlayerExtraMeepleCard,
  CollectionExtraMeepleCard,
  AgentExtraMeepleCard,
} from './EntityExtraMeepleCard';
export {
  ExtraMeepleCardDrawer,
  DrawerLoadingSkeleton,
  DrawerErrorState,
} from './ExtraMeepleCardDrawer';
export type { DrawerEntityType, ExtraMeepleCardDrawerProps } from './ExtraMeepleCardDrawer';
export { MediaTab } from './tabs/MediaTab';
export { AITab } from './tabs/AITab';
export { InteractiveCardDeck } from './tabs/InteractiveCardDeck';
export { InteractiveTimer } from './tabs/InteractiveTimer';
export { EventsTimeline } from './tabs/EventsTimeline';
export { TurnPhaseIndicator } from './tabs/TurnPhaseIndicator';
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
  AgentDetailData,
  ChatThreadPreview,
  KbDocumentPreview,
  CardZone,
  CardEntry,
  CardDeckState,
  CardDeckActions,
  TimerStatus,
  TimerState,
  TimerActions,
  SessionEventType,
  EnhancedTimelineEvent,
  EventsTimelineData,
  EventsTimelineActions,
  TurnPhaseState,
  // SharedGame admin types
  SharedGameDocumentInfo,
  SharedGameKbCardInfo,
  SharedGameDetailData,
  SharedGameExtraMeepleCardTab,
  SharedGameExtraMeepleCardProps,
} from './types';
