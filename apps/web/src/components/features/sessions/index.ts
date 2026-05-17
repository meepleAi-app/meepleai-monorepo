/**
 * Barrel export for /sessions v2 components (Wave D.1, Issue #735).
 *
 * Single import point for the orchestrator SessionsLibraryView and E2E specs.
 * All components are pure (no useTranslation) — labels injected by orchestrator.
 */

export { ConnectionChipStripFooter } from '@/components/features/sessions/ConnectionChipStripFooter';
export type {
  ConnectionChip,
  ConnectionChipStripFooterProps,
} from '@/components/features/sessions/ConnectionChipStripFooter';

export { EmptySessions } from '@/components/features/sessions/EmptySessions';
export type {
  EmptySessionsKind,
  EmptySessionsLabels,
  EmptySessionsProps,
} from '@/components/features/sessions/EmptySessions';

export { OutcomeBadge } from '@/components/features/sessions/OutcomeBadge';
export type { OutcomeBadgeLabels } from '@/components/features/sessions/OutcomeBadge';

export { ScoringInline } from '@/components/features/sessions/ScoringInline';

export { SessionCardGrid } from '@/components/features/sessions/SessionCardGrid';
export type {
  SessionCardGridLabels,
  SessionCardGridProps,
} from '@/components/features/sessions/SessionCardGrid';

export { SessionCardList } from '@/components/features/sessions/SessionCardList';
export type {
  SessionCardListLabels,
  SessionCardListProps,
} from '@/components/features/sessions/SessionCardList';

export { SessionsFilters } from '@/components/features/sessions/SessionsFilters';
export type {
  SessionsFiltersLabels,
  SessionsFiltersProps,
} from '@/components/features/sessions/SessionsFilters';

export { SessionsHero } from '@/components/features/sessions/SessionsHero';
export type {
  SessionsHeroLabels,
  SessionsHeroProps,
} from '@/components/features/sessions/SessionsHero';
