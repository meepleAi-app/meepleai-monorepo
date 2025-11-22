/**
 * Chat Configuration
 *
 * Centralized configuration for chat-related features including
 * thread limits, title generation, and message editing timeouts.
 *
 * All values are type-safe using `as const` to enable literal type inference.
 */

export const CHAT_CONFIG = {
  /**
   * Maximum number of active threads allowed per game
   * When exceeded, the oldest thread will be automatically archived
   * @default 5
   */
  MAX_THREADS_PER_GAME: 5,

  /**
   * Maximum length for auto-generated thread titles
   * Longer titles will be truncated with ellipsis
   * @default 50
   */
  AUTO_TITLE_MAX_LENGTH: 50,

  /**
   * Timeout for message edit operations (milliseconds)
   * After this time, edit operations will be cancelled
   * @default 5000
   */
  MESSAGE_EDIT_TIMEOUT_MS: 5000,

  /**
   * Timeout for optimistic UI updates (milliseconds)
   * If backend confirmation doesn't arrive within this time, rollback occurs
   * @default 3000
   */
  OPTIMISTIC_UPDATE_TIMEOUT_MS: 3000,

  /**
   * Maximum length for search result preview text
   * Used to truncate long messages in search results
   * @default 100
   */
  SEARCH_PREVIEW_MAX_LENGTH: 100,
} as const;

/**
 * Type-safe keys for CHAT_CONFIG
 */
export type ChatConfigKey = keyof typeof CHAT_CONFIG;
