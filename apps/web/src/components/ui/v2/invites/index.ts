/**
 * Public barrel for `/invites/[token]` v2 component family (Wave A.5b, #611).
 *
 * These components are scoped to the `/invites/[token]` page-client and its
 * tests. Do NOT import them from other surfaces — re-create or extend
 * existing primitives if you need similar functionality elsewhere, otherwise
 * the `invites/*` directory will accrete unrelated variants and leak into
 * unrelated pages.
 */

export { AcceptedSuccessShell } from './accepted-success-shell';
export type { AcceptedSuccessShellProps } from './accepted-success-shell';

export { Confetti } from './confetti';
export type { ConfettiProps } from './confetti';

export { DeclinedShell } from './declined-shell';
export type { DeclinedShellProps } from './declined-shell';

export { ErrorBanner } from './error-banner';
export type { ErrorBannerProps, ErrorBannerTone } from './error-banner';

export { InviteHero } from './hero';
export type { InviteHeroProps } from './hero';

export { InviteHostCard } from './invite-host-card';
export type { InviteHostCardProps } from './invite-host-card';

export { SessionMetaGrid } from './session-meta-grid';
export type { SessionMetaGridProps, SessionMetaItem } from './session-meta-grid';
