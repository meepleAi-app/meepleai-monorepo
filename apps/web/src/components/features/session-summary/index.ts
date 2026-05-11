/**
 * Barrel export for /sessions/[id] summary v2 components (Wave D.3, Issue #756).
 *
 * Single import point for the orchestrator SessionSummaryView and E2E specs.
 * All components are pure (no `useTranslation`) — labels injected by the
 * orchestrator. Keep this file alphabetised and aligned with the contract
 * §5 component spec list.
 */

export { AchievementsCarousel } from '@/components/features/session-summary/AchievementsCarousel';
export type {
  AchievementsCarouselLabels,
  AchievementsCarouselProps,
} from '@/components/features/session-summary/AchievementsCarousel';

export { ChatHighlights } from '@/components/features/session-summary/ChatHighlights';
export type {
  ChatHighlight,
  ChatHighlightsLabels,
  ChatHighlightsProps,
} from '@/components/features/session-summary/ChatHighlights';

export { Confetti } from '@/components/features/session-summary/Confetti';
export type { ConfettiProps } from '@/components/features/session-summary/Confetti';

export { ConnectionBar } from '@/components/features/session-summary/ConnectionBar';
export type {
  ConnectionBarEntity,
  ConnectionBarLabels,
  ConnectionBarPip,
  ConnectionBarProps,
} from '@/components/features/session-summary/ConnectionBar';

export { PhotosGallery } from '@/components/features/session-summary/PhotosGallery';
export type {
  PhotosGalleryLabels,
  PhotosGalleryProps,
} from '@/components/features/session-summary/PhotosGallery';

export { PlayAgainCta } from '@/components/features/session-summary/PlayAgainCta';
export type {
  PlayAgainCtaLabels,
  PlayAgainCtaProps,
} from '@/components/features/session-summary/PlayAgainCta';

export { ScoringBreakdownTable } from '@/components/features/session-summary/ScoringBreakdownTable';
export type {
  ScoringBreakdownTableLabels,
  ScoringBreakdownTableProps,
} from '@/components/features/session-summary/ScoringBreakdownTable';

export { SessionDiaryTimeline } from '@/components/features/session-summary/SessionDiaryTimeline';
export type {
  DiaryFilter,
  DiaryTurnGroup,
  SessionDiaryTimelineLabels,
  SessionDiaryTimelineProps,
} from '@/components/features/session-summary/SessionDiaryTimeline';

export { SessionKpiGrid } from '@/components/features/session-summary/SessionKpiGrid';
export type {
  KpiEntityHint,
  KpiEntry,
  SessionKpiGridProps,
} from '@/components/features/session-summary/SessionKpiGrid';

export { SessionShareCard } from '@/components/features/session-summary/SessionShareCard';
export type {
  SessionShareCardLabels,
  SessionShareCardProps,
  ShareCardTheme,
  ShareChannel,
} from '@/components/features/session-summary/SessionShareCard';

export { SessionSummaryHero } from '@/components/features/session-summary/SessionSummaryHero';
export type {
  SessionSummaryHeroLabels,
  SessionSummaryHeroProps,
} from '@/components/features/session-summary/SessionSummaryHero';
