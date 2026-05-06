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
 */

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
