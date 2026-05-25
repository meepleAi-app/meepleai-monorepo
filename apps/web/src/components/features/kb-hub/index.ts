/**
 * kb-hub feature module barrel (Issue #1481)
 *
 * Pure presentational components for the /library/[gameId]/kb route.
 * Orchestrator and hooks live separately.
 */

export { EmptyState } from './EmptyState';
export type { EmptyStateLabels, EmptyStateProps } from './EmptyState';

export { PdfRow } from './PdfRow';
export type { KbPdf, PdfStatus, PdfRowLabels, PdfRowProps } from './PdfRow';

export { KbStatsCard } from './KbStatsCard';
export type { CoverageLevel, KbStatsCardLabels, KbStatsCardProps } from './KbStatsCard';

export { RaptorPanel } from './RaptorPanel';
export type { RaptorTier, RaptorPanelLabels, RaptorPanelProps } from './RaptorPanel';

export { ActionsMenu } from './ActionsMenu';
export type {
  PdfAction,
  ActionsMenuItemLabel,
  ActionsMenuLabels,
  ActionsMenuProps,
  ActionsMenuPdfHeader,
} from './ActionsMenu';

export { DeleteDialog } from './DeleteDialog';
export type {
  DeleteDialogCleanupItem,
  DeleteDialogLabels,
  DeleteDialogProps,
} from './DeleteDialog';

export { ReindexModal } from './ReindexModal';
export type {
  ReindexPhase,
  ReindexCostRow,
  ReindexModalLabels,
  ReindexModalProgress,
  ReindexModalSummary,
  ReindexModalProps,
} from './ReindexModal';

export { HubDefault } from './HubDefault';
export type {
  HubDefaultGameInfo,
  HubDefaultStatsStripLabels,
  HubDefaultColumnHeaders,
  HubDefaultCoverageLabels,
  HubDefaultLabels,
  HubDefaultProps,
} from './HubDefault';
