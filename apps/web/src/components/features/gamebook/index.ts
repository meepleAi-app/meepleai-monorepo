/**
 * Barrel export for /gamebook v2 components (SP6 Phase B Task 2, Issue #788).
 *
 * Single import point for the orchestrator GamebookIndexView (Task 3) and
 * E2E specs. All Phase B Task 2 components are pure (no useTranslation) —
 * labels injected by orchestrator via the Wave D.3 pre-resolved-labels
 * pattern.
 *
 * SP6 Phase C.1.B Task B (Issue #789) appends the 5 read-only wizard
 * components for /gamebook/upload Tier L Foundation sub-PR.
 *
 * `TranslateParagraphDemo` (Phase A Path 5a Nanolith demo workaround) was
 * dropped by #1306 after #747 sequence shipped the by-paragraph endpoint
 * via PR-B (`0ba93671a`) and the FE hook via PR #1307. The translation flow
 * in production is owned by `TranslateViewer` + `useTranslateSegmentSSE`.
 */

// Phase B (SP6 #788)
export { GamebookHero } from '@/components/features/gamebook/GamebookHero';
export type {
  GamebookHeroLabels,
  GamebookHeroProps,
} from '@/components/features/gamebook/GamebookHero';

export { QuotaWidget } from '@/components/features/gamebook/QuotaWidget';
export type {
  QuotaWidgetLabels,
  QuotaWidgetProps,
  QuotaWidgetVariant,
} from '@/components/features/gamebook/QuotaWidget';

export { GamebookCard } from '@/components/features/gamebook/GamebookCard';
export type {
  GamebookCardLabels,
  GamebookCardProps,
} from '@/components/features/gamebook/GamebookCard';

export { EmptyGamebooks } from '@/components/features/gamebook/EmptyGamebooks';
export type {
  EmptyGamebooksLabels,
  EmptyGamebooksProps,
} from '@/components/features/gamebook/EmptyGamebooks';

export { GamebookCardSkeleton } from '@/components/features/gamebook/GamebookCardSkeleton';
export type { GamebookCardSkeletonProps } from '@/components/features/gamebook/GamebookCardSkeleton';

// Phase C.1.B Task B — /gamebook/upload wizard read-only components (SP6 #789)
export { StepIndicator } from '@/components/features/gamebook/StepIndicator';
export type {
  StepIndicatorLabels,
  StepIndicatorProps,
} from '@/components/features/gamebook/StepIndicator';

export { GameSearchBar } from '@/components/features/gamebook/GameSearchBar';
export type {
  GameSearchBarLabels,
  GameSearchBarProps,
} from '@/components/features/gamebook/GameSearchBar';

export { GameSearchCard } from '@/components/features/gamebook/GameSearchCard';
export type {
  GameSearchCardLabels,
  GameSearchCardProps,
} from '@/components/features/gamebook/GameSearchCard';

export { NoResultsPanel } from '@/components/features/gamebook/NoResultsPanel';
export type {
  NoResultsPanelLabels,
  NoResultsPanelProps,
} from '@/components/features/gamebook/NoResultsPanel';

export { ActionCard } from '@/components/features/gamebook/ActionCard';
export type { ActionCardProps } from '@/components/features/gamebook/ActionCard';

// Phase C.2.B Task B — /gamebook/upload wizard interactive components (SP6 #789)
export { CameraViewfinder } from '@/components/features/gamebook/CameraViewfinder';
export type {
  CameraViewfinderLabels,
  CameraViewfinderProps,
} from '@/components/features/gamebook/CameraViewfinder';

export { PageThumb } from '@/components/features/gamebook/PageThumb';
export type { PageThumbLabels, PageThumbProps } from '@/components/features/gamebook/PageThumb';

export { ConfidenceBadge } from '@/components/features/gamebook/ConfidenceBadge';
export type {
  ConfidenceBadgeLabels,
  ConfidenceBadgeProps,
} from '@/components/features/gamebook/ConfidenceBadge';

export { OfflineBanner } from '@/components/features/gamebook/OfflineBanner';
export type {
  OfflineBannerLabels,
  OfflineBannerProps,
} from '@/components/features/gamebook/OfflineBanner';

export { CancelModal } from '@/components/features/gamebook/CancelModal';
export type {
  CancelModalLabels,
  CancelModalProps,
} from '@/components/features/gamebook/CancelModal';

export { DesktopDropFallback } from '@/components/features/gamebook/DesktopDropFallback';
export type {
  DesktopDropFallbackLabels,
  DesktopDropFallbackProps,
} from '@/components/features/gamebook/DesktopDropFallback';

// Iter 1.A — /play/[campaignId] shell
export { GamebookPlayShell } from '@/components/features/gamebook/GamebookPlayShell';
export type { GamebookPlayShellProps } from '@/components/features/gamebook/GamebookPlayShell';

// Iter 1.A — New campaign dialog + Nanolith CTA
export { NewCampaignDialog } from '@/components/features/gamebook/NewCampaignDialog';
export type { NewCampaignDialogProps } from '@/components/features/gamebook/NewCampaignDialog';

export { NanolithCampaignCTA } from '@/components/features/gamebook/NanolithCampaignCTA';
export type { NanolithCampaignCTAProps } from '@/components/features/gamebook/NanolithCampaignCTA';

// Iter 1.B — translate viewer + sub-components
export { TranslateViewer } from '@/components/features/gamebook/TranslateViewer';
export type { TranslateViewerProps } from '@/components/features/gamebook/TranslateViewer';

export { SegmentPicker } from '@/components/features/gamebook/SegmentPicker';
export type { SegmentPickerProps } from '@/components/features/gamebook/SegmentPicker';

export { TranslationPane } from '@/components/features/gamebook/TranslationPane';
export type { TranslationPaneProps } from '@/components/features/gamebook/TranslationPane';

// SP6 Iter 1.B — quota/credits checkout modal (#953)
export { CheckoutModal } from '@/components/features/gamebook/CheckoutModal';
export type {
  CheckoutLabels,
  CheckoutModalProps,
  CheckoutQuota,
} from '@/components/features/gamebook/CheckoutModal';

export { SoftWarningCredits } from '@/components/features/gamebook/SoftWarningCredits';
export type {
  SoftWarningCreditsLabels,
  SoftWarningCreditsProps,
} from '@/components/features/gamebook/SoftWarningCredits';
