/**
 * Stars — fractional rating display for `/toolkits/[id]`.
 *
 * Wave 3 (#1145). Lifted to canonical `@/components/ui/feedback/Stars` in
 * #1469 to make the primitive reusable cross-feature (game-detail BGG
 * rating, player-detail favorite-rating, etc.). This file is now a thin
 * re-export to preserve existing imports.
 */

export { Stars, type StarsProps } from '@/components/ui/feedback/Stars';
