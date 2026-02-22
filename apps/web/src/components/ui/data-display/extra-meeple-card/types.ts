/**
 * ExtraMeepleCard Types
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 *
 * Types for the expanded card component used in session detail views.
 * ~600×900px card with 4-tab system (Overview, Toolkit, Scoreboard, History).
 */

import type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionTimelineEvent,
  SessionMediaCounts,
  SessionActionHandlers,
} from '../meeple-card-features/session-types';
import type { LucideIcon } from 'lucide-react';
import type { ChatStatus } from '../meeple-card-features/ChatStatusBadge';

// Re-export session types for convenience
export type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionTimelineEvent,
  SessionMediaCounts,
  SessionActionHandlers,
};

// ============================================================================
// Tab System
// ============================================================================

/** Available tabs in the ExtraMeepleCard */
export type ExtraMeepleCardTab = 'overview' | 'toolkit' | 'scoreboard' | 'history' | 'media' | 'ai';

/** Tab configuration */
export interface TabConfig {
  id: ExtraMeepleCardTab;
  label: string;
  icon: LucideIcon;
  badge?: number;
}

// ============================================================================
// Toolkit Types (mirrors backend GameToolkit DTOs)
// ============================================================================

/** Dice tool configuration */
export interface DiceToolInfo {
  name: string;
  diceType: string;
  quantity: number;
  customFaces?: string[];
  isInteractive: boolean;
  color?: string;
}

/** Card tool configuration */
export interface CardToolInfo {
  name: string;
  deckType: string;
  cardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

/** Timer tool configuration */
export interface TimerToolInfo {
  name: string;
  durationSeconds: number;
  timerType: string;
  autoStart: boolean;
  color?: string;
  isPerPlayer: boolean;
  warningThresholdSeconds?: number;
}

/** Counter tool configuration */
export interface CounterToolInfo {
  name: string;
  minValue: number;
  maxValue: number;
  defaultValue: number;
  isPerPlayer: boolean;
  icon?: string;
  color?: string;
}

/** Scoring template */
export interface ScoringTemplateInfo {
  dimensions: string[];
  defaultUnit: string;
  scoreType: string;
}

/** Turn template */
export interface TurnTemplateInfo {
  turnOrderType: string;
  phases: string[];
}

/** Complete toolkit data */
export interface ToolkitData {
  id: string;
  name: string;
  version: number;
  isPublished: boolean;
  diceTools: DiceToolInfo[];
  cardTools: CardToolInfo[];
  timerTools: TimerToolInfo[];
  counterTools: CounterToolInfo[];
  scoringTemplate?: ScoringTemplateInfo;
  turnTemplate?: TurnTemplateInfo;
}

// ============================================================================
// Snapshot Types (mirrors backend SessionSnapshot DTOs)
// ============================================================================

/** Session snapshot for history tab */
export interface SnapshotInfo {
  id: string;
  snapshotNumber: number;
  triggerType: string;
  description: string;
  turnNumber?: number;
  createdAt: string;
}

// ============================================================================
// Tab Content Data
// ============================================================================

/** Overview tab data */
export interface OverviewTabData {
  gameName: string;
  gameImageUrl?: string;
  status: SessionStatus;
  startedAt: string;
  completedAt?: string;
  durationMinutes?: number;
  sessionCode?: string;
  players: SessionPlayerInfo[];
  turnInfo?: SessionTurnInfo;
  currentRound?: number;
  totalRounds?: number;
}

/** Scoreboard tab data */
export interface ScoreboardTabData {
  players: SessionPlayerInfo[];
  roundScores: SessionRoundScore[];
  scoringConfig?: SessionScoringConfig;
  totalRounds?: number;
}

/** History tab data */
export interface HistoryTabData {
  snapshots: SnapshotInfo[];
  timeline: SessionTimelineEvent[];
  totalTurns?: number;
}

// ============================================================================
// Media Tab Types (Issue #4762)
// ============================================================================

/** Media item (photo or note) */
export interface MediaItem {
  id: string;
  type: 'photo' | 'note';
  url?: string;
  thumbnailUrl?: string;
  title?: string;
  content?: string;
  turnNumber?: number;
  createdAt: string;
  createdBy?: string;
}

/** Media tab data */
export interface MediaTabData {
  items: MediaItem[];
  totalPhotos: number;
  totalNotes: number;
}

// ============================================================================
// AI Tab Types (Issue #4762)
// ============================================================================

/** Chat message in the AI tab */
export interface AIChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  sources?: AISource[];
}

/** Source citation from RAG agent */
export interface AISource {
  title: string;
  snippet: string;
  documentId?: string;
}

/** Quick action for the AI tab */
export interface AIQuickAction {
  label: string;
  prompt: string;
  icon?: LucideIcon;
}

/** AI tab data */
export interface AITabData {
  messages: AIChatMessage[];
  sessionContext?: string;
  quickActions: AIQuickAction[];
  isLoading?: boolean;
  agentName?: string;
  agentModel?: string;
}

// ============================================================================
// Entity Variant Types (Issue #4762)
// ============================================================================

/** Entity types supported by ExtraMeepleCard variants */
export type ExtraMeepleCardEntity = 'session' | 'game' | 'player' | 'collection';

/** Compact agent preview for the GameExtraMeepleCard Agent tab (Issue #5029) */
export interface GameAgentPreview {
  id: string;
  name: string;
  model?: string;
  isActive: boolean;
}

/** Game entity detail data */
export interface GameDetailData {
  id: string;
  title: string;
  imageUrl?: string;
  publisher?: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playTimeMinutes?: number;
  description?: string;
  averageRating?: number;
  totalPlays?: number;
  faqCount?: number;
  rulesDocumentCount?: number;
  /** KB documents indexed for this game (Issue #5029) */
  kbDocuments?: KbDocumentPreview[];
  /** Agent configured for this game (Issue #5029) */
  agent?: GameAgentPreview;
}

/** Player entity detail data */
export interface PlayerDetailData {
  id: string;
  displayName: string;
  avatarUrl?: string;
  gamesPlayed: number;
  winRate: number;
  totalSessions: number;
  favoriteGame?: string;
  achievements: { id: string; name: string; icon: string }[];
  recentGames: { name: string; date: string; result: 'win' | 'loss' | 'draw' }[];
}

/** Collection entity detail data */
export interface CollectionDetailData {
  id: string;
  name: string;
  description?: string;
  imageUrl?: string;
  gameCount: number;
  ownerName: string;
  isShared: boolean;
  games: { id: string; title: string; imageUrl?: string }[];
}

/** Agent entity detail data (Issue #5026 — AgentExtraMeepleCard) */
export interface AgentDetailData {
  id: string;
  name: string;
  /** Agent type e.g. 'qa', 'assistant' */
  type: string;
  /** Strategy name e.g. 'hybrid-rag' */
  strategyName: string;
  strategyParameters: Record<string, unknown>;
  isActive: boolean;
  /** True when the agent has not been invoked recently */
  isIdle: boolean;
  invocationCount: number;
  lastInvokedAt: string | null;
  createdAt: string;
  /** ID of the game this agent is associated with */
  gameId?: string;
  /** Display name of the linked game */
  gameName?: string;
}

/** Chat thread preview for the History tab (Issue #5026) */
export interface ChatThreadPreview {
  id: string;
  createdAt: string;
  messageCount: number;
  firstMessagePreview: string;
}

/** KB document preview for the KB tab (Issue #5026) */
export interface KbDocumentPreview {
  id: string;
  fileName: string;
  uploadedAt: string;
  status: 'processing' | 'indexed' | 'failed' | 'none';
}

/** Chat status — re-exported from ChatStatusBadge to avoid duplication */
export type { ChatStatus } from '../meeple-card-features/ChatStatusBadge';

/** Single message in a chat thread (Issue #5027 — ChatExtraMeepleCard) */
export interface ChatDetailMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

/** Chat thread detail data (Issue #5027 — ChatExtraMeepleCard) */
export interface ChatDetailData {
  id: string;
  status: ChatStatus;
  /** ID of the agent that handles this thread */
  agentId?: string;
  /** Display name of the linked agent */
  agentName?: string;
  /** Model identifier for the agent */
  agentModel?: string;
  /** ID of the game used as context */
  gameId?: string;
  /** Display name of the context game */
  gameName?: string;
  /** Thumbnail URL of the context game */
  gameThumbnailUrl?: string;
  /** ISO timestamp when the thread was started */
  startedAt: string;
  /** Total duration of the thread in minutes */
  durationMinutes?: number;
  /** Total number of messages in the thread */
  messageCount: number;
  /** Last N messages (fetched with ?limit=10) */
  messages: ChatDetailMessage[];
  /** LLM temperature parameter */
  temperature?: number;
  /** LLM max tokens parameter */
  maxTokens?: number;
  /** System prompt used for this thread */
  systemPrompt?: string;
}

/** KB document detail data (Issue #5028 — KbExtraMeepleCard) */
export interface KbDetailData {
  id: string;
  /** ID of the game this document belongs to */
  gameId?: string;
  /** Display name of the linked game */
  gameName?: string;
  /** Thumbnail URL of the linked game */
  gameThumbnailUrl?: string;
  /** Original filename */
  fileName: string;
  /** File size in bytes */
  fileSize?: number;
  /** Number of pages */
  pageCount?: number;
  /** Total character count */
  characterCount?: number;
  /** ISO timestamp when the document was uploaded */
  uploadedAt?: string;
  /** ISO timestamp when indexing completed (processedAt from API) */
  processedAt?: string;
  /** Current indexing status */
  status: 'processing' | 'indexed' | 'failed' | 'none';
  /** Error message when status === 'failed' */
  errorMessage?: string;
  /** First ~500 words of extracted text */
  extractedContent?: string;
  /** Whether the document has more content beyond extractedContent */
  hasMoreContent?: boolean;
}

// ============================================================================
// Interactive Card Deck Types (Issue #4763)
// ============================================================================

/** Card zone in the deck system */
export type CardZone = 'deck' | 'hand' | 'discard' | 'play';

/** Single card in the deck */
export interface CardEntry {
  id: string;
  name: string;
  zone: CardZone;
  faceUp: boolean;
  order: number;
}

/** Card deck runtime state */
export interface CardDeckState {
  toolName: string;
  deckType: string;
  cards: CardEntry[];
  deckCount: number;
  handCount: number;
  discardCount: number;
  shuffleable: boolean;
  allowDraw: boolean;
  allowDiscard: boolean;
  allowPeek: boolean;
  allowReturnToDeck: boolean;
}

/** Card deck action handlers */
export interface CardDeckActions {
  onDraw?: () => void;
  onDiscard?: (cardId: string) => void;
  onShuffle?: () => void;
  onPeek?: () => void;
  onReturnToDeck?: (cardId: string) => void;
}

// ============================================================================
// Interactive Timer Types (Issue #4763)
// ============================================================================

/** Timer running state */
export type TimerStatus = 'idle' | 'running' | 'paused' | 'warning' | 'expired';

/** Single timer runtime state */
export interface TimerState {
  toolName: string;
  timerType: string;
  totalSeconds: number;
  remainingSeconds: number;
  status: TimerStatus;
  color?: string;
  isPerPlayer: boolean;
  warningThresholdSeconds?: number;
  /** Per-player remaining seconds (keyed by playerId) */
  playerTimers?: Record<string, { remainingSeconds: number; status: TimerStatus }>;
  activePlayerId?: string;
}

/** Timer action handlers */
export interface TimerActions {
  onStart?: () => void;
  onPause?: () => void;
  onResume?: () => void;
  onReset?: () => void;
}

// ============================================================================
// Events Timeline Types (Issue #4763)
// ============================================================================

/** Extended event types for timeline filtering */
export type SessionEventType = 'system' | 'turn' | 'score' | 'action' | 'media' | 'phase' | 'snapshot' | 'chat';

/** Enhanced timeline event with expanded details */
export interface EnhancedTimelineEvent {
  id: string;
  timestamp: string;
  type: SessionEventType;
  label: string;
  description?: string;
  playerId?: string;
  playerName?: string;
  turnNumber?: number;
  snapshotId?: string;
}

/** Events timeline data */
export interface EventsTimelineData {
  events: EnhancedTimelineEvent[];
  totalEvents: number;
}

/** Events timeline action handlers */
export interface EventsTimelineActions {
  onEventClick?: (event: EnhancedTimelineEvent) => void;
  onNavigateToSnapshot?: (snapshotId: string) => void;
}

// ============================================================================
// Turn Phase Indicator Types (Issue #4763)
// ============================================================================

/** Turn phase state */
export interface TurnPhaseState {
  phases: string[];
  currentPhaseIndex: number;
  currentTurnNumber: number;
  totalTurns?: number;
}

// ============================================================================
// Component Props
// ============================================================================

// ============================================================================
// SharedGame Admin Types
// ============================================================================

/** Document info from SharedGameDocument API */
export interface SharedGameDocumentInfo {
  id: string;
  /** PDF document ID — used by PdfIndexingStatus */
  pdfDocumentId: string;
  /** Numeric type: 0=Rulebook, 1=Errata, 2=Homerule */
  documentType: number;
  version: string;
  isActive: boolean;
  tags: string[];
  createdAt: string;
}

/** KB card info from KbCardDto API */
export interface SharedGameKbCardInfo {
  id: string;
  pdfDocumentId: string;
  fileName: string;
  indexingStatus: string; // 'pending'|'processing'|'completed'|'failed'
  chunkCount: number;
  indexedAt: string | null;
  documentType: string | null;
  version: string | null;
  isActive: boolean;
}

/** Aggregated detail data for the SharedGame admin card */
export interface SharedGameDetailData extends GameDetailData {
  status: string;
  documents: SharedGameDocumentInfo[];
  kbCards: SharedGameKbCardInfo[];
  linkedAgent: { id: string; name: string; isActive: boolean } | null;
}

export type SharedGameExtraMeepleCardTab = 'details' | 'documents' | 'kb-cards';

export interface SharedGameExtraMeepleCardProps {
  data: SharedGameDetailData;
  onUploadPdf?: () => void;
  onCreateAgent?: () => void;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

/** ExtraMeepleCard component props */
export interface ExtraMeepleCardProps {
  /** Session ID */
  sessionId: string;
  /** Session title (game name or custom) */
  title: string;
  /** Session status */
  status: SessionStatus;
  /** Game cover image URL */
  imageUrl?: string;
  /** Active elapsed time display (mm:ss or hh:mm:ss) */
  elapsedTime?: string;
  /** Player count */
  playerCount: number;
  /** Current tab */
  activeTab?: ExtraMeepleCardTab;
  /** Tab change callback */
  onTabChange?: (tab: ExtraMeepleCardTab) => void;
  /** Action handlers */
  actions?: SessionActionHandlers;
  /** Tab badge counts */
  tabBadges?: Partial<Record<ExtraMeepleCardTab, number>>;

  /** Overview tab data (inline or via hook) */
  overviewData?: OverviewTabData;
  /** Toolkit data */
  toolkitData?: ToolkitData;
  /** Scoreboard data */
  scoreboardData?: ScoreboardTabData;
  /** History data */
  historyData?: HistoryTabData;
  /** Media tab data (Issue #4762) */
  mediaData?: MediaTabData;
  /** AI tab data (Issue #4762) */
  aiData?: AITabData;
  /** Callback when user sends an AI message */
  onAISendMessage?: (message: string) => void;

  /** Loading state */
  loading?: boolean;
  /** Error state */
  error?: string;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}
