/**
 * Barrel exports for /game-nights/[id] feature surface (Issue #951).
 *
 * Foundation commit: placeholder file claiming the canonical path
 * `apps/web/src/components/features/game-night-detail/` per Stage-2 #1025.
 *
 * Subsequent commits populate this barrel with:
 *   - GameNightDetailHero
 *   - GameNightDetailTabs (Dettagli / Voting / Chat)
 *   - RSVPRow / RSVPCTABottomSheet
 *   - GameVoteCard / VotingTiedResolver
 *   - GameNightChatStream
 *   - CancelledBanner / InProgressCTA / CompletedCTA / HostReadyCTA
 *
 * See mockup: admin-mockups/design_files/sp7-game-night-detail-rsvp.{html,jsx}
 */

export {
  GameNightAvatar,
  computeInitials,
  deriveHueFromId,
  type GameNightAvatarProps,
} from './GameNightAvatar';
export { GameNightStatusBadge, type GameNightStatusBadgeProps } from './GameNightStatusBadge';
export { GameNightRsvpRow, type GameNightRsvpRowProps } from './GameNightRsvpRow';
export {
  GameNightRsvpActionBar,
  type GameNightRsvpActionBarLabels,
  type GameNightRsvpActionBarProps,
} from './GameNightRsvpActionBar';
export {
  GameNightDetailHero,
  type GameNightDetailHeroLabels,
  type GameNightDetailHeroProps,
} from './GameNightDetailHero';
export {
  GameNightCancelledBanner,
  type GameNightCancelledBannerLabels,
  type GameNightCancelledBannerProps,
} from './GameNightCancelledBanner';
export {
  PublicRsvpForm,
  PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH,
  type PublicRsvpFormLabels,
  type PublicRsvpFormProps,
} from './PublicRsvpForm';
export {
  InvalidTokenError,
  type InvalidTokenErrorLabels,
  type InvalidTokenErrorProps,
} from './error-states/InvalidTokenError';
export {
  ExpiredOrCancelledError,
  type ExpiredOrCancelledErrorLabels,
  type ExpiredOrCancelledErrorProps,
  type ExpiredOrCancelledKind,
} from './error-states/ExpiredOrCancelledError';
export {
  RateLimitedError,
  type RateLimitedErrorLabels,
  type RateLimitedErrorProps,
} from './error-states/RateLimitedError';
export {
  GenericError,
  type GenericErrorLabels,
  type GenericErrorProps,
} from './error-states/GenericError';
