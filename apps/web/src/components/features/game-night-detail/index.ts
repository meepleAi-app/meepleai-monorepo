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
