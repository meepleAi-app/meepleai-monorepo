/**
 * MeepleCard hooks
 *
 * Re-exports all extracted hooks for convenient importing.
 *
 * @module components/ui/data-display/meeple-card/hooks
 */

export { useMobileInteraction } from './useMobileInteraction';
export { useCardAnimation } from './useCardAnimation';
export { useCardTheme } from './useCardTheme';

export type {
  UseMobileInteractionOptions,
  UseMobileInteractionReturn,
} from './useMobileInteraction';
export type { UseCardAnimationReturn } from './useCardAnimation';
