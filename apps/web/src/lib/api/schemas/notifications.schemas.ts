/**
 * Notification API Schemas (Issue #2053)
 *
 * Zod schemas for validating user notification responses.
 * User notification system for upload/processing completion feedback.
 */

import { z } from 'zod';

// Notification type (event category)
export const NotificationTypeSchema = z.enum([
  'pdf_upload_completed',
  'rule_spec_generated',
  'processing_failed',
  'new_comment',
  'shared_link_accessed',
]);
export type NotificationType = z.infer<typeof NotificationTypeSchema>;

// Notification severity levels (matches backend NotificationSeverity)
export const NotificationSeveritySchema = z.enum(['info', 'success', 'warning', 'error']);
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
