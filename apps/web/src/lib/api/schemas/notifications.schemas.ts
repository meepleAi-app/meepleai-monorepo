/**
 * Notification API Schemas (Issue #2053)
 *
 * Zod schemas for validating user notification responses.
 * User notification system for upload/processing completion feedback.
 */

import { z } from 'zod';

// Known notification types (for component logic — not used for validation)
export const KNOWN_NOTIFICATION_TYPES = [
  'pdf_upload_completed',
  'rule_spec_generated',
  'processing_failed',
  'new_comment',
  'shared_link_accessed',
  'processing_job_completed',
  'share_request_created',
  'share_request_approved',
  'share_request_rejected',
  'share_request_changes_requested',
  'admin_new_share_request',
  'admin_shared_game_submitted',
  'admin_openrouter_daily_summary',
  'admin_openrouter_rpm_alert',
  'admin_openrouter_budget_alert',
  'admin_circuit_breaker_state_changed',
  'badge_earned',
  'rate_limit_approaching',
  'rate_limit_reached',
  'cooldown_ended',
  'loan_reminder',
  'session_terminated',
  'game_proposal_in_review',
  'game_proposal_kb_merged',
  'processing_job_failed',
  'agent_linked',
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
  createdAt: z.string().datetime(),
  readAt: z.string().datetime().nullable().optional(),
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
});
export type NotificationPreferences = z.infer<typeof NotificationPreferencesSchema>;

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
