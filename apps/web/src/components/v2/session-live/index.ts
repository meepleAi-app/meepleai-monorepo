/**
 * session-live component barrel export — Wave D.2 Foundation sub-PR (Issue #746).
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
 * Interactions sub-PR adds:
 *   - SessionToolsRail, LiveAgentChat, LiveSessionNotes
 *   - RightColumnTabs, PauseOverlay, EndgameDialog, ConnectionLostBanner
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
