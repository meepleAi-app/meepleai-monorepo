/**
 * Wave C.1 (Issue #581) — `/games/[id]` v2 component family barrel.
 *
 * Centralised re-exports so `GameDetailViewV2` (Task 3) and downstream
 * page-clients can import from `@/components/v2/game-detail` without churning
 * deep paths.
 */

export { GameDetailHero } from './GameDetailHero';
export type {
  GameDetailHeroLabels,
  GameDetailHeroMeta,
  GameDetailHeroProps,
  GameDetailHeroVariant,
} from './GameDetailHero';

export { GameDetailTabsAnimated, tabIdFor, panelIdFor } from './GameDetailTabsAnimated';
export type {
  GameDetailTabConfig,
  GameDetailTabsAnimatedProps,
  TabKey,
} from './GameDetailTabsAnimated';

export { GameDetailKpiCards } from './GameDetailKpiCards';
export type { GameDetailKpiCard, GameDetailKpiCardsProps } from './GameDetailKpiCards';

export { GameDetailFaqList } from './GameDetailFaqList';
export type {
  GameDetailFaqEntry,
  GameDetailFaqListLabels,
  GameDetailFaqListProps,
} from './GameDetailFaqList';

export { GameDetailRulesAccordion } from './GameDetailRulesAccordion';
export type {
  GameDetailRuleSection,
  GameDetailRulesAccordionLabels,
  GameDetailRulesAccordionProps,
} from './GameDetailRulesAccordion';

export { GameDetailSessionsRail } from './GameDetailSessionsRail';
export type {
  GameDetailSessionEntry,
  GameDetailSessionsRailLabels,
  GameDetailSessionsRailProps,
} from './GameDetailSessionsRail';

export { GameDetailAgentsList } from './GameDetailAgentsList';
export type {
  AgentsState,
  GameDetailAgentEntry,
  GameDetailAgentsListLabels,
  GameDetailAgentsListProps,
} from './GameDetailAgentsList';

export { GameDetailKbDocList } from './GameDetailKbDocList';
export type {
  GameDetailKbDocEntry,
  GameDetailKbDocListLabels,
  GameDetailKbDocListProps,
  GameDetailKbStatus,
} from './GameDetailKbDocList';
