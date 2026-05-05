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

export { ActionLogTimeline } from './ActionLogTimeline';
export type {
  ActionLogEntry,
  ActionLogTimelineLabels,
  ActionLogTimelineProps,
} from './ActionLogTimeline';

export { DesktopBody } from './DesktopBody';
export type { DesktopBodyProps } from './DesktopBody';

export { LiveScoringPanel } from './LiveScoringPanel';
export type {
  LiveScoringPanelLabels,
  LiveScoringPanelProps,
  LiveScoringPanelScoreEntry,
} from './LiveScoringPanel';

export { LiveTopBar } from './LiveTopBar';
export type { LiveTopBarLabels, LiveTopBarProps } from './LiveTopBar';

export { MobileBody } from './MobileBody';
export type { MobileBodyLabels, MobileBodyProps, MobileTab } from './MobileBody';

export { PlayerRosterLive } from './PlayerRosterLive';
export type {
  LivePlayerEntry,
  PlayerRosterLiveLabels,
  PlayerRosterLiveProps,
} from './PlayerRosterLive';

export { TurnIndicator } from './TurnIndicator';
export type { TurnIndicatorLabels, TurnIndicatorProps } from './TurnIndicator';

// ─── Interactions sub-PR (Issue #750) ─────────────────────────────────────────

export { ConnectionLostBanner } from './ConnectionLostBanner';
export type {
  ConnectionLostBannerKind,
  ConnectionLostBannerLabels,
  ConnectionLostBannerProps,
} from './ConnectionLostBanner';

export { EndgameDialog } from './EndgameDialog';
export type { EndgameDialogLabels, EndgameDialogProps, FinalScoreEntry } from './EndgameDialog';

export { LiveAgentChat } from './LiveAgentChat';
export type { ChatMessage, LiveAgentChatLabels, LiveAgentChatProps } from './LiveAgentChat';

export { LiveSessionNotes } from './LiveSessionNotes';
export type { LiveSessionNotesLabels, LiveSessionNotesProps, NoteEntry } from './LiveSessionNotes';

export { PauseOverlay } from './PauseOverlay';
export type { PauseOverlayLabels, PauseOverlayProps } from './PauseOverlay';

export { RightColumnTabs } from './RightColumnTabs';
export type { LiveTab, RightColumnTabsLabels, RightColumnTabsProps } from './RightColumnTabs';

export { SessionToolsRail } from './SessionToolsRail';
export type { SessionToolsRailLabels, SessionToolsRailProps } from './SessionToolsRail';
