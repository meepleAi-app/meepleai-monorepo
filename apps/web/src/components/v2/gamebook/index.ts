/**
 * Barrel export for /gamebook v2 components (SP6 Phase B Task 2, Issue #788).
 *
 * Single import point for the orchestrator GamebookIndexView (Task 3) and
 * E2E specs. All Phase B Task 2 components are pure (no useTranslation) —
 * labels injected by orchestrator via the Wave D.3 pre-resolved-labels
 * pattern.
 *
 * `TranslateParagraphDemo` is the Phase A demo component (kept exported for
 * the Nanolith one-shot flow under /gamebook/[gameId]/play).
 *
 * SP6 Phase C.1.B Task B (Issue #789) appends the 5 read-only wizard
 * components for /gamebook/upload Tier L Foundation sub-PR.
 */

// Phase B (SP6 #788)
export { GamebookHero } from './GamebookHero';
export type { GamebookHeroLabels, GamebookHeroProps } from './GamebookHero';

export { QuotaWidget } from './QuotaWidget';
export type { QuotaWidgetLabels, QuotaWidgetProps, QuotaWidgetVariant } from './QuotaWidget';

export { GamebookCard } from './GamebookCard';
export type { GamebookCardLabels, GamebookCardProps } from './GamebookCard';

export { EmptyGamebooks } from './EmptyGamebooks';
export type { EmptyGamebooksLabels, EmptyGamebooksProps } from './EmptyGamebooks';

export { GamebookCardSkeleton } from './GamebookCardSkeleton';
export type { GamebookCardSkeletonProps } from './GamebookCardSkeleton';

// Phase A demo component (preserved):
export { TranslateParagraphDemo } from './TranslateParagraphDemo';
export type { TranslateParagraphDemoProps } from './TranslateParagraphDemo';

// Phase C.1.B Task B — /gamebook/upload wizard read-only components (SP6 #789)
export { StepIndicator } from './StepIndicator';
export type { StepIndicatorLabels, StepIndicatorProps } from './StepIndicator';

export { GameSearchBar } from './GameSearchBar';
export type { GameSearchBarLabels, GameSearchBarProps } from './GameSearchBar';

export { GameSearchCard } from './GameSearchCard';
export type { GameSearchCardLabels, GameSearchCardProps } from './GameSearchCard';

export { NoResultsPanel } from './NoResultsPanel';
export type { NoResultsPanelLabels, NoResultsPanelProps } from './NoResultsPanel';

export { ActionCard } from './ActionCard';
export type { ActionCardProps } from './ActionCard';

// Phase C.2.B Task B — /gamebook/upload wizard interactive components (SP6 #789)
export { CameraViewfinder } from './CameraViewfinder';
export type { CameraViewfinderLabels, CameraViewfinderProps } from './CameraViewfinder';

export { PageThumb } from './PageThumb';
export type { PageThumbLabels, PageThumbProps } from './PageThumb';

export { ConfidenceBadge } from './ConfidenceBadge';
export type { ConfidenceBadgeLabels, ConfidenceBadgeProps } from './ConfidenceBadge';

export { OfflineBanner } from './OfflineBanner';
export type { OfflineBannerLabels, OfflineBannerProps } from './OfflineBanner';

export { CancelModal } from './CancelModal';
export type { CancelModalLabels, CancelModalProps } from './CancelModal';

export { DesktopDropFallback } from './DesktopDropFallback';
export type { DesktopDropFallbackLabels, DesktopDropFallbackProps } from './DesktopDropFallback';

// Iter 1.A — /play/[campaignId] shell
export { GamebookPlayShell } from './GamebookPlayShell';
export type { GamebookPlayShellProps } from './GamebookPlayShell';

// Iter 1.A — New campaign dialog + Nanolith CTA
export { NewCampaignDialog } from './NewCampaignDialog';
export type { NewCampaignDialogProps } from './NewCampaignDialog';

export { NanolithCampaignCTA } from './NanolithCampaignCTA';
export type { NanolithCampaignCTAProps } from './NanolithCampaignCTA';

// Iter 1.B — translate viewer + sub-components
export { TranslateViewer } from './TranslateViewer';
export type { TranslateViewerProps } from './TranslateViewer';

export { SegmentPicker } from './SegmentPicker';
export type { SegmentPickerProps } from './SegmentPicker';

export { TranslationPane } from './TranslationPane';
export type { TranslationPaneProps } from './TranslationPane';
