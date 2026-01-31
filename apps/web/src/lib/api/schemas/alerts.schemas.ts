/**
 * Alert API Schemas (Issue #921)
 *
 * Zod schemas for validating Alert system responses.
 * Minimal UI implementation: list alerts, resolve action.
 */

import { z } from 'zod';

// Alert severity levels (matches backend AlertSeverity)
export const AlertSeveritySchema = z.enum(['info', 'warning', 'error', 'critical']);
export type AlertSeverity = z.infer<typeof AlertSeveritySchema>;

// Alert DTO matching backend Contract
export const AlertDtoSchema = z.object({
  id: z.string().uuid(),
  alertType: z.string(),
  severity: AlertSeveritySchema,
  message: z.string(),
  metadata: z.record(z.string(), z.unknown()).nullable().optional(),
  triggeredAt: z.string().datetime(),
  resolvedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean(),
  channelSent: z.record(z.string(), z.boolean()).nullable().optional(),
});

export type AlertDto = z.infer<typeof AlertDtoSchema>;

// Response schemas
export const GetAlertsResponseSchema = z.array(AlertDtoSchema);
export type GetAlertsResponse = z.infer<typeof GetAlertsResponseSchema>;

export const ResolveAlertResponseSchema = z.object({
  message: z.string(),
});
export type ResolveAlertResponse = z.infer<typeof ResolveAlertResponseSchema>;
