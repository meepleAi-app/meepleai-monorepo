/**
 * Public barrel for `/invites/[token]` v2 component family (Wave A.5b, #611).
 *
 * These components are scoped to the `/invites/[token]` page-client and its
 * tests. Do NOT import them from other surfaces — re-create or extend
 * existing primitives if you need similar functionality elsewhere, otherwise
 * the `invites/*` directory will accrete unrelated variants and leak into
 * unrelated pages.
 */

export { AcceptedSuccessShell } from '@/components/ui/invites/accepted-success-shell';
export type { AcceptedSuccessShellProps } from '@/components/ui/invites/accepted-success-shell';

export { Confetti } from '@/components/ui/invites/confetti';
export type { ConfettiProps } from '@/components/ui/invites/confetti';

export { DeclinedShell } from '@/components/ui/invites/declined-shell';
export type { DeclinedShellProps } from '@/components/ui/invites/declined-shell';

export { ErrorBanner } from '@/components/ui/invites/error-banner';
export type { ErrorBannerProps, ErrorBannerTone } from '@/components/ui/invites/error-banner';

export { InviteHero } from '@/components/ui/invites/hero';
export type { InviteHeroProps } from '@/components/ui/invites/hero';

export { InviteHostCard } from '@/components/ui/invites/invite-host-card';
export type { InviteHostCardProps } from '@/components/ui/invites/invite-host-card';

export { SessionMetaGrid } from '@/components/ui/invites/session-meta-grid';
export type {
  SessionMetaGridProps,
  SessionMetaItem,
} from '@/components/ui/invites/session-meta-grid';
