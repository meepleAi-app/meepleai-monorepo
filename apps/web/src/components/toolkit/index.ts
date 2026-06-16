/**
 * Toolkit Components
 *
 * Game session toolkit widgets for the GameToolkit bounded context.
 * Issue #5128 — Epic B.
 */

export { ToolkitDashboard } from './ToolkitDashboard';
export { WidgetCard } from './WidgetCard';
export { RandomGeneratorWidget } from './RandomGeneratorWidget';
export { TurnManagerWidget } from './TurnManagerWidget';
export { ScoreTrackerWidget } from './ScoreTrackerWidget';
export { ResourceManagerWidget } from './ResourceManagerWidget';
export { NoteManagerWidget } from './NoteManagerWidget';
export { WhiteboardWidget } from './WhiteboardWidget';
export { CardDeckTool } from './CardDeckTool';
export { CounterTool } from './CounterTool';
// AI-config preview renderers (B19-4a, issue #1749). Renamed in #2418 to
// disambiguate from the live-runtime polymorphic renderers under
// `features/session-live/` (PR #2411 G5b, PR #2416 G5c). See audit
// `claudedocs/2026-06-16-toolkit-vs-session-live-duplication-audit.md`.
export { ToolkitAiScoringPreviewRenderer } from './ToolkitAiScoringPreviewRenderer';
export type { ToolkitAiScoringPreviewRendererProps } from './ToolkitAiScoringPreviewRenderer';
export { ToolkitAiTurnPreviewRenderer } from './ToolkitAiTurnPreviewRenderer';
export type { ToolkitAiTurnPreviewRendererProps } from './ToolkitAiTurnPreviewRenderer';
