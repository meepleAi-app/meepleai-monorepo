/**
 * Notification API Schemas (Issue #2053)
 *
 * Zod schemas for validating user notification responses.
 * User notification system for upload/processing completion feedback.
 */

import { z } from 'zod';

// Known notification types (for component logic — not used for validation)
export const KNOWN_NOTIFICATION_TYPES = [
  'document_ready',
  'rule_spec_generated',
  'document_processing_failed',
  'shared_link_accessed',
  'share_request_created',
  'share_request_approved',
  'share_request_rejected',
  'share_request_changes_requested',
  'admin_new_share_request',
  'admin_shared_game_submitted',
  'admin_openrouter_daily_summary',
  'admin_openrouter_threshold_alert',
  'admin_system_health_alert',
  'admin_model_status_changed',
  'badge_earned',
  'rate_limit_approaching',
  'rate_limit_reached',
  'cooldown_ended',
  'loan_reminder',
  'session_terminated',
  'game_proposal_in_review',
  'game_proposal_kb_merged',
  'agent_ready',
  'game_night_invitation',
  'game_night_rsvp_received',
  'game_night_published',
  'game_night_cancelled',
  'game_night_reminder',
] as const;

// Defensive schema — accepts any string to prevent API breaking on new backend types
export const NotificationTypeSchema = z.string();
export type NotificationType = z.infer<typeof NotificationTypeSchema>;

// Notification severity levels — defensive string to handle future backend additions
export const NotificationSeveritySchema = z.string();
export type NotificationSeverity = z.infer<typeof NotificationSeveritySchema>;

// Notification DTO matching backend contract
export const NotificationDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  type: NotificationTypeSchema,
  severity: NotificationSeveritySchema,
  title: z.string(),
  message: z.string(),
  link: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(), // JSON string
  isRead: z.boolean(),
  createdAt: z.string().datetime({ offset: true }),
  readAt: z.string().datetime({ offset: true }).nullable().optional(),
});

export type NotificationDto = z.infer<typeof NotificationDtoSchema>;

// Response schemas
export const GetNotificationsResponseSchema = z.array(NotificationDtoSchema);
export type GetNotificationsResponse = z.infer<typeof GetNotificationsResponseSchema>;

export const GetUnreadCountResponseSchema = z.object({
  count: z.number().int().nonnegative(),
});
export type GetUnreadCountResponse = z.infer<typeof GetUnreadCountResponseSchema>;

// Issue #4220: Notification Preferences
export const NotificationPreferencesSchema = z.object({
  userId: z.string().uuid(),
  emailOnDocumentReady: z.boolean(),
  emailOnDocumentFailed: z.boolean(),
  emailOnRetryAvailable: z.boolean(),
  pushOnDocumentReady: z.boolean(),
  pushOnDocumentFailed: z.boolean(),
  pushOnRetryAvailable: z.boolean(),
  inAppOnDocumentReady: z.boolean(),
  inAppOnDocumentFailed: z.boolean(),
  inAppOnRetryAvailable: z.boolean(),
  hasPushSubscription: z.boolean(),
  // Game Night preferences (Issue #33 / #44 / #47)
  inAppOnGameNightInvitation: z.boolean().optional().default(true),
  emailOnGameNightInvitation: z.boolean().optional().default(true),
  pushOnGameNightInvitation: z.boolean().optional().default(true),
  emailOnGameNightReminder: z.boolean().optional().default(true),
  pushOnGameNightReminder: z.boolean().optional().default(true),
  // #535 / #2832: opt-in admin email when a mechanic card is suppressed (default off).
  emailOnCardSuppressed: z.boolean().optional().default(false),
  // Quiet hours (ADR-076 / Issue #2995). Times are "HH:mm" strings; null when not configured.
  // Server-side gating suppresses email + Slack DM during the window (in-app is never suppressed).
  timeZone: z.string().optional().default('UTC'),
  quietHoursStart: z.string().nullable().optional(),
  quietHoursEnd: z.string().nullable().optional(),
  // Slack channel preferences (Issue #2994) — persisted via PUT /notifications/preferences/slack.
  slackEnabled: z.boolean().optional().default(true),
  slackOnDocumentReady: z.boolean().optional().default(true),
  slackOnDocumentFailed: z.boolean().optional().default(true),
  slackOnRetryAvailable: z.boolean().optional().default(false),
  slackOnGameNightInvitation: z.boolean().optional().default(true),
  slackOnGameNightReminder: z.boolean().optional().default(true),
  slackOnShareRequestCreated: z.boolean().optional().default(true),
  slackOnShareRequestApproved: z.boolean().optional().default(true),
  slackOnBadgeEarned: z.boolean().optional().default(true),
});
export type NotificationPreferences = z.infer<typeof NotificationPreferencesSchema>;

// Issue #2994: dedicated payloads for the Slack + quiet-hours save endpoints.
export const SlackPreferencesInputSchema = NotificationPreferencesSchema.pick({
  slackEnabled: true,
  slackOnDocumentReady: true,
  slackOnDocumentFailed: true,
  slackOnRetryAvailable: true,
  slackOnGameNightInvitation: true,
  slackOnGameNightReminder: true,
  slackOnShareRequestCreated: true,
  slackOnShareRequestApproved: true,
  slackOnBadgeEarned: true,
});
export type SlackPreferencesInput = z.infer<typeof SlackPreferencesInputSchema>;

export const QuietHoursInputSchema = z.object({
  timeZone: z.string(),
  quietHoursStart: z.string().nullable(),
  quietHoursEnd: z.string().nullable(),
});
export type QuietHoursInput = z.infer<typeof QuietHoursInputSchema>;

export const MarkNotificationReadResponseSchema = z.object({
  success: z.boolean(),
});
export type MarkNotificationReadResponse = z.infer<typeof MarkNotificationReadResponseSchema>;

export const MarkAllNotificationsReadResponseSchema = z.object({
  updatedCount: z.number().int().nonnegative(),
});
export type MarkAllNotificationsReadResponse = z.infer<
  typeof MarkAllNotificationsReadResponseSchema
>;
