/**
 * Wave B.2 (Issue #634) — v2 agents component barrel.
 *
 * Re-exports the 4 in-scope feature components for the `/agents` route v2
 * brownfield migration. The orphan stubs `AgentDetailPanel` and
 * `AgentsSidebarList` are intentionally NOT re-exported — they remain out of
 * Wave B.2 scope and will be addressed in a follow-up cleanup pass after the
 * Wave B umbrella closes (#580).
 *
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md §3.2
 */

export { AgentsHero } from './AgentsHero';
export type { AgentsHeroLabels, AgentsHeroProps, AgentsHeroStat } from './AgentsHero';

export { AgentFilters } from './AgentFilters';
export type {
  AgentFiltersLabels,
  AgentFiltersProps,
  AgentsSortKey,
  AgentsStatusKey,
} from './AgentFilters';

export { AgentsResultsGrid } from './AgentsResultsGrid';
export type { AgentsResultsGridProps } from './AgentsResultsGrid';

export { EmptyAgents } from './EmptyAgents';
export type {
  EmptyAgentsCopy,
  EmptyAgentsKind,
  EmptyAgentsLabels,
  EmptyAgentsProps,
} from './EmptyAgents';
