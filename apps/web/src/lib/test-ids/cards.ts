/**
 * Test ID constants for Card components.
 *
 * Covers: MeepleCard, FlipCard
 *
 * Import from '@/lib/test-ids' in both components and tests.
 */

export const CARD_TEST_IDS = {
  // MeepleCard
  card: 'meeple-card',
  skeleton: 'meeple-card-skeleton',

  // FlipCard
  flipContainer: 'meeple-card-flip-container',
  flipFront: 'meeple-card-front',
  flipBack: 'meeple-card-back',
  flipButton: 'meeple-card-flip-button',
} as const;
