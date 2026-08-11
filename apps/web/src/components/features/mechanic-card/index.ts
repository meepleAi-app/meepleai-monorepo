/**
 * Public mechanic card (ME-M1.6, Issue #528) component family barrel.
 *
 * Lets the `/games/[id]/card` route shell import from
 * `@/components/features/mechanic-card` without deep paths.
 */

export { MechanicCardView } from '@/components/features/mechanic-card/MechanicCardView';
export type { MechanicCardViewProps } from '@/components/features/mechanic-card/MechanicCardView';

export { MechanicCitationBadge } from '@/components/features/mechanic-card/MechanicCitationBadge';
export type { MechanicCitationBadgeProps } from '@/components/features/mechanic-card/MechanicCitationBadge';

export { MechanicCitationPanel } from '@/components/features/mechanic-card/MechanicCitationPanel';
export type { MechanicCitationPanelProps } from '@/components/features/mechanic-card/MechanicCitationPanel';

export { ClaimFeedbackControls } from '@/components/features/mechanic-card/ClaimFeedbackControls';
export type {
  ClaimFeedbackControlsProps,
  ClaimVote,
} from '@/components/features/mechanic-card/ClaimFeedbackControls';

export { ReportErrorDialog } from '@/components/features/mechanic-card/ReportErrorDialog';
export type {
  ReportErrorDialogProps,
  ReportErrorSubmission,
} from '@/components/features/mechanic-card/ReportErrorDialog';
