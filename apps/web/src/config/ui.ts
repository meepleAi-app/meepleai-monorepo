/**
 * UI Configuration
 *
 * Centralized configuration for UI animations, transitions, and toast notifications.
 * All durations are in milliseconds unless otherwise specified.
 *
 * All values are type-safe using `as const` to enable literal type inference.
 */

export const UI_CONFIG = {
  /**
   * Sidebar collapse/expand animation duration (milliseconds)
   * @default 300
   */
  SIDEBAR_COLLAPSE_DURATION_MS: 300,

  /**
   * Toast notification display duration (milliseconds)
   * Transient toasts auto-dismiss after this time
   * @default 5000
   */
  TOAST_DURATION_MS: 5000,

  /**
   * Modal dialog animation duration (milliseconds)
   * Used for fade-in/fade-out transitions
   * @default 200
   */
  MODAL_ANIMATION_DURATION_MS: 200,

  /**
   * Sheet close animation duration (milliseconds)
   * @default 300
   */
  SHEET_ANIMATION_DURATION_CLOSED_MS: 300,

  /**
   * Sheet open animation duration (milliseconds)
   * @default 500
   */
  SHEET_ANIMATION_DURATION_OPEN_MS: 500,

  /**
   * Maximum number of automatic retries for error recovery
   * @default 3
   */
  ERROR_DISPLAY_MAX_RETRIES: 3,

  /**
   * Maximum retry delay for exponential backoff (milliseconds)
   * Prevents excessive wait times during error recovery
   * @default 5000
   */
  ERROR_DISPLAY_MAX_RETRY_DELAY_MS: 5000,

  /**
   * Base delay for error display retry (milliseconds)
   * Used with exponential backoff: baseDelay * 2^attempt
   * @default 1000
   */
  ERROR_DISPLAY_RETRY_BASE_DELAY_MS: 1000,
} as const;

/**
 * Type-safe keys for UI_CONFIG
 */
export type UiConfigKey = keyof typeof UI_CONFIG;
