/**
 * Detail-layout primitives.
 *
 * Wave A.4 (Issue #603) shipped the pieces (Hero, Tabs, StickyCta,
 * ContributorsStrip, AgentListItem, KbDocItem, ToolkitListItem, and the
 * EmptyState / ErrorState / NotFoundState family).
 *
 * Stage 3 (Issue #1026) adds the DetailPageLayout composer that arranges
 * these pieces with WAI-ARIA landmarks. The composer is consumer-agnostic:
 * each slot accepts a ReactNode, so callers can use any of the primitives
 * above (or none) without the composer knowing.
 */

export { AgentListItem } from '@/components/ui/detail-layout/agent-list-item';
export type {
  AgentListItemLabels,
  AgentListItemProps,
} from '@/components/ui/detail-layout/agent-list-item';

export { ContributorsStrip } from '@/components/ui/detail-layout/contributors-strip';
export type {
  ContributorAvatar,
  ContributorsStripLabels,
  ContributorsStripProps,
} from '@/components/ui/detail-layout/contributors-strip';

export { EmptyState } from '@/components/ui/detail-layout/empty-state';
export type {
  EmptyStateKind,
  EmptyStateLabels,
  EmptyStateProps,
} from '@/components/ui/detail-layout/empty-state';

export { ErrorState } from '@/components/ui/detail-layout/error-state';
export type { ErrorStateLabels, ErrorStateProps } from '@/components/ui/detail-layout/error-state';

export { Hero } from '@/components/ui/detail-layout/hero';
export type { HeroLabels, HeroProps } from '@/components/ui/detail-layout/hero';

export { KbDocItem } from '@/components/ui/detail-layout/kb-doc-item';
export type {
  KbDocItemLabels,
  KbDocItemProps,
  KbDocKind,
} from '@/components/ui/detail-layout/kb-doc-item';

export { NotFoundState } from '@/components/ui/detail-layout/not-found-state';
export type {
  NotFoundStateLabels,
  NotFoundStateProps,
} from '@/components/ui/detail-layout/not-found-state';

export { StickyCta } from '@/components/ui/detail-layout/sticky-cta';
export type { StickyCtaLabels, StickyCtaProps } from '@/components/ui/detail-layout/sticky-cta';

export { Tabs, TAB_KEYS, tabId, tabPanelId } from '@/components/ui/detail-layout/tabs';
export type {
  TabDescriptor,
  TabKey,
  TabsLabels,
  TabsProps,
} from '@/components/ui/detail-layout/tabs';

export { ToolkitListItem } from '@/components/ui/detail-layout/toolkit-list-item';
export type {
  ToolkitListItemLabels,
  ToolkitListItemProps,
} from '@/components/ui/detail-layout/toolkit-list-item';

export { DetailPageLayout } from './DetailPageLayout';
export type { DetailPageLayoutProps } from './DetailPageLayout';
