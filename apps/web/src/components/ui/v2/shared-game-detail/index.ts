/**
 * Wave A.4 (Issue #603) — V2 component family for /shared-games/[id].
 *
 * Public API barrel. Consumed by `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx`
 * and Vitest unit tests under `apps/web/src/__tests__/components/ui/v2/shared-game-detail/`.
 */

export { AgentListItem } from './agent-list-item';
export type { AgentListItemLabels, AgentListItemProps } from './agent-list-item';

export { ContributorsStrip } from './contributors-strip';
export type {
  ContributorAvatar,
  ContributorsStripLabels,
  ContributorsStripProps,
} from './contributors-strip';

export { EmptyState } from './empty-state';
export type { EmptyStateKind, EmptyStateLabels, EmptyStateProps } from './empty-state';

export { Hero } from './hero';
export type { HeroLabels, HeroProps } from './hero';

export { KbDocItem } from './kb-doc-item';
export type { KbDocItemLabels, KbDocItemProps, KbDocKind } from './kb-doc-item';

export { StickyCta } from './sticky-cta';
export type { StickyCtaLabels, StickyCtaProps } from './sticky-cta';

export { Tabs, TAB_KEYS, tabId, tabPanelId } from './tabs';
export type { TabDescriptor, TabKey, TabsLabels, TabsProps } from './tabs';

export { ToolkitListItem } from './toolkit-list-item';
export type { ToolkitListItemLabels, ToolkitListItemProps } from './toolkit-list-item';
