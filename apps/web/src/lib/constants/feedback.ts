/**
 * Feedback Constants
 *
 * Centralized constants for agent feedback outcomes.
 * These values MUST match the backend validation in ProvideAgentFeedbackCommandHandler.
 *
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ProvideAgentFeedbackCommandHandler.cs
 */

/**
 * Valid feedback outcome values.
 * Backend validates against these exact strings (case-sensitive).
 */
export const FEEDBACK_OUTCOMES = {
  HELPFUL: 'helpful',
  NOT_HELPFUL: 'not-helpful', // Note: hyphen, not underscore
  INCORRECT: 'incorrect',
} as const;

/**
 * Type for feedback outcome values
 */
export type FeedbackOutcome = (typeof FEEDBACK_OUTCOMES)[keyof typeof FEEDBACK_OUTCOMES];
