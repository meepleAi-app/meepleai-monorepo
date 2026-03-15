/**
 * Admin Notification Schemas
 *
 * Zod schemas for admin manual notification endpoints.
 */

import { z } from 'zod';

export const SendManualNotificationRequestSchema = z.object({
  title: z.string().min(1).max(200),
  message: z.string().min(1).max(2000),
  channels: z.array(z.enum(['inapp', 'email'])).min(1),
  recipientType: z.enum(['all', 'role', 'users']),
  recipientRole: z.string().optional(),
  recipientUserIds: z.array(z.string().uuid()).optional(),
  deepLinkPath: z.string().optional(),
});

export const SendManualNotificationResultSchema = z.object({
  totalRecipients: z.number().int(),
  dispatched: z.number().int(),
  skipped: z.number().int(),
  wasCapped: z.boolean(),
});

export type SendManualNotificationRequest = z.infer<typeof SendManualNotificationRequestSchema>;
export type SendManualNotificationResult = z.infer<typeof SendManualNotificationResultSchema>;
