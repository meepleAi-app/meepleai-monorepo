/**
 * Wave C.1 (Issue #581) — `/games/[id]` v2 component family barrel.
 *
 * Centralised re-exports so `GameDetailViewV2` (Task 3) and downstream
 * page-clients can import from `@/components/features/game-detail` without churning
 * deep paths.
 */

export { GameDetailHero } from '@/components/features/game-detail/GameDetailHero';
export type {
  GameDetailHeroLabels,
  GameDetailHeroMeta,
  GameDetailHeroProps,
  GameDetailHeroVariant,
} from '@/components/features/game-detail/GameDetailHero';

export { GameDetailTabsAnimated, tabIdFor, panelIdFor } from '@/components/features/game-detail/GameDetailTabsAnimated';
export type {
  GameDetailTabConfig,
  GameDetailTabsAnimatedProps,
  TabKey,
} from '@/components/features/game-detail/GameDetailTabsAnimated';

export { GameDetailKpiCards } from '@/components/features/game-detail/GameDetailKpiCards';
export type { GameDetailKpiCard, GameDetailKpiCardsProps } from '@/components/features/game-detail/GameDetailKpiCards';

export { GameDetailFaqList } from '@/components/features/game-detail/GameDetailFaqList';
export type {
  GameDetailFaqEntry,
  GameDetailFaqListLabels,
  GameDetailFaqListProps,
} from '@/components/features/game-detail/GameDetailFaqList';

export { GameDetailRulesAccordion } from '@/components/features/game-detail/GameDetailRulesAccordion';
export type {
  GameDetailRuleSection,
  GameDetailRulesAccordionLabels,
  GameDetailRulesAccordionProps,
} from '@/components/features/game-detail/GameDetailRulesAccordion';

export { GameDetailSessionsRail } from '@/components/features/game-detail/GameDetailSessionsRail';
export type {
  GameDetailSessionEntry,
  GameDetailSessionsRailLabels,
  GameDetailSessionsRailProps,
} from '@/components/features/game-detail/GameDetailSessionsRail';

export { GameDetailAgentsList } from '@/components/features/game-detail/GameDetailAgentsList';
export type {
  AgentsState,
  GameDetailAgentEntry,
  GameDetailAgentsListLabels,
  GameDetailAgentsListProps,
} from '@/components/features/game-detail/GameDetailAgentsList';

export { GameDetailKbDocList } from '@/components/features/game-detail/GameDetailKbDocList';
export type {
  GameDetailKbDocEntry,
  GameDetailKbDocListLabels,
  GameDetailKbDocListProps,
  GameDetailKbStatus,
} from '@/components/features/game-detail/GameDetailKbDocList';
