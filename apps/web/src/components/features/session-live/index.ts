/**
 * session-live component barrel export — Wave D.2 (Issues #746 + #750).
 *
 * Foundation scope (7 components + 2 layout shells):
 *   - LiveTopBar: sticky header with role-based CTAs
 *   - TurnIndicator: progress bar + active player display
 *   - PlayerRosterLive: participant list with online status
 *   - LiveScoringPanel: read-only scoreboard (Interactions sub-PR adds write)
 *   - ActionLogTimeline: append-only event log
 *   - DesktopBody: 3-column layout shell (lg+)
 *   - MobileBody: bottom-nav tab layout (< lg)
 *
 * Interactions sub-PR (Issue #750) adds:
 *   - SessionToolsRail: tool grid (Player+Host only)
 *   - LiveAgentChat: chat panel with visibility toggle
 *   - LiveSessionNotes: notes panel with add form
 *   - RightColumnTabs: desktop tablist (tools/chat/notes) with keyboard nav
 *   - ConnectionLostBanner: SSE connection state indicator
 *   - PauseOverlay: modal dialog with focus trap + ESC closes
 *   - EndgameDialog: modal dialog with focus trap + ESC DISABLED (intentional)
 *
 * All components DIVERGE from MeepleCard (Gate C — live/real-time UI).
 */

export { ActionLogTimeline } from '@/components/features/session-live/ActionLogTimeline';
export type {
  ActionLogEntry,
  ActionLogTimelineLabels,
  ActionLogTimelineProps,
} from '@/components/features/session-live/ActionLogTimeline';

export { DesktopBody } from '@/components/features/session-live/DesktopBody';
export type { DesktopBodyProps } from '@/components/features/session-live/DesktopBody';

export { LiveScoringPanel } from '@/components/features/session-live/LiveScoringPanel';
export type {
  LiveScoringPanelLabels,
  LiveScoringPanelProps,
  LiveScoringPanelScoreEntry,
} from '@/components/features/session-live/LiveScoringPanel';

export { LiveTopBar } from '@/components/features/session-live/LiveTopBar';
export type {
  LiveTopBarLabels,
  LiveTopBarProps,
} from '@/components/features/session-live/LiveTopBar';

export { MobileBody } from '@/components/features/session-live/MobileBody';
export type {
  MobileBodyLabels,
  MobileBodyProps,
  MobileTab,
} from '@/components/features/session-live/MobileBody';

export { PlayerRosterLive } from '@/components/features/session-live/PlayerRosterLive';
export type {
  LivePlayerEntry,
  PlayerRosterLiveLabels,
  PlayerRosterLiveProps,
} from '@/components/features/session-live/PlayerRosterLive';

export { TurnIndicator } from '@/components/features/session-live/TurnIndicator';
export type {
  TurnIndicatorLabels,
  TurnIndicatorProps,
} from '@/components/features/session-live/TurnIndicator';

// ─── Interactions sub-PR (Issue #750) ─────────────────────────────────────────

export { ConnectionLostBanner } from '@/components/features/session-live/ConnectionLostBanner';
export type {
  ConnectionLostBannerKind,
  ConnectionLostBannerLabels,
  ConnectionLostBannerProps,
} from '@/components/features/session-live/ConnectionLostBanner';

export { EndgameDialog } from '@/components/features/session-live/EndgameDialog';
export type {
  EndgameDialogLabels,
  EndgameDialogProps,
  FinalScoreEntry,
} from '@/components/features/session-live/EndgameDialog';

export { LiveAgentChat } from '@/components/features/session-live/LiveAgentChat';
export type {
  ChatMessage,
  LiveAgentChatLabels,
  LiveAgentChatProps,
} from '@/components/features/session-live/LiveAgentChat';

export { LiveSessionNotes } from '@/components/features/session-live/LiveSessionNotes';
export type {
  LiveSessionNotesLabels,
  LiveSessionNotesProps,
  NoteEntry,
} from '@/components/features/session-live/LiveSessionNotes';

export { PauseOverlay } from '@/components/features/session-live/PauseOverlay';
export type {
  PauseOverlayLabels,
  PauseOverlayProps,
} from '@/components/features/session-live/PauseOverlay';

export { RightColumnTabs } from '@/components/features/session-live/RightColumnTabs';
export type {
  LiveTab,
  RightColumnTabsLabels,
  RightColumnTabsProps,
} from '@/components/features/session-live/RightColumnTabs';

export { SessionToolsRail } from '@/components/features/session-live/SessionToolsRail';
export type {
  SessionToolsRailLabels,
  SessionToolsRailProps,
} from '@/components/features/session-live/SessionToolsRail';
