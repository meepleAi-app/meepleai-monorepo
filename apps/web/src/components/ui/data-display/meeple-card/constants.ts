/**
 * MeepleCard Constants
 *
 * Shared constants for the MeepleCard component system.
 * Re-exports values from meeple-card-styles and defines component-level defaults.
 *
 * @module components/ui/data-display/meeple-card/constants
 */

// Re-export entity colors and drawer map from styles
export { entityColors, DRAWER_ENTITY_TYPE_MAP } from '../meeple-card-styles';

// Re-export CVA variant definitions (used by sub-components)
export { meepleCardVariants, contentVariants, coverVariants } from '../meeple-card-styles';

// ============================================================================
// Component Defaults
// ============================================================================

/** Default rating scale maximum */
export const DEFAULT_RATING_MAX = 5;

/** Default max visible tags before overflow */
export const DEFAULT_MAX_VISIBLE_TAGS = 3;

/** HoverPreview delay in milliseconds */
export const HOVER_PREVIEW_DELAY_MS = 500;
