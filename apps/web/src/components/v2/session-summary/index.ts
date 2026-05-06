/**
 * Barrel export for /sessions/[id] summary v2 components (Wave D.3, Issue #756).
 *
 * Single import point for the orchestrator SessionSummaryView and E2E specs.
 * All components are pure (no `useTranslation`) — labels injected by the
 * orchestrator. Keep this file alphabetised and aligned with the contract
 * §5 component spec list.
 */

export { AchievementsCarousel } from './AchievementsCarousel';
export type { AchievementsCarouselLabels, AchievementsCarouselProps } from './AchievementsCarousel';

export { ChatHighlights } from './ChatHighlights';
export type { ChatHighlight, ChatHighlightsLabels, ChatHighlightsProps } from './ChatHighlights';

export { Confetti } from './Confetti';
export type { ConfettiProps } from './Confetti';

export { ConnectionBar } from './ConnectionBar';
export type {
  ConnectionBarEntity,
  ConnectionBarLabels,
  ConnectionBarPip,
  ConnectionBarProps,
} from './ConnectionBar';

export { PhotosGallery } from './PhotosGallery';
export type { PhotosGalleryLabels, PhotosGalleryProps } from './PhotosGallery';

export { PlayAgainCta } from './PlayAgainCta';
export type { PlayAgainCtaLabels, PlayAgainCtaProps } from './PlayAgainCta';

export { ScoringBreakdownTable } from './ScoringBreakdownTable';
export type {
  ScoringBreakdownTableLabels,
  ScoringBreakdownTableProps,
} from './ScoringBreakdownTable';

export { SessionDiaryTimeline } from './SessionDiaryTimeline';
export type {
  DiaryFilter,
  DiaryTurnGroup,
  SessionDiaryTimelineLabels,
  SessionDiaryTimelineProps,
} from './SessionDiaryTimeline';

export { SessionKpiGrid } from './SessionKpiGrid';
export type { KpiEntityHint, KpiEntry, SessionKpiGridProps } from './SessionKpiGrid';

export { SessionShareCard } from './SessionShareCard';
export type {
  SessionShareCardLabels,
  SessionShareCardProps,
  ShareCardTheme,
  ShareChannel,
} from './SessionShareCard';

export { SessionSummaryHero } from './SessionSummaryHero';
export type { SessionSummaryHeroLabels, SessionSummaryHeroProps } from './SessionSummaryHero';
