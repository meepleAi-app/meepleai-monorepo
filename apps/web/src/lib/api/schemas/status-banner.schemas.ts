/**
 * Status Banner Zod schemas (Issue #1089).
 *
 * Mirrors backend DTOs in:
 *  apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/DTOs/
 *
 * Wire contract:
 *  - GET  /api/v1/status-banner       → 200 PublicStatusBannerResponse | 204 No Content
 *  - GET  /api/v1/admin/status-banner → 200 AdminStatusBannerResponse
 *  - PUT  /api/v1/admin/status-banner → 200 AdminStatusBannerResponse
 */

import { z } from 'zod';

export const StatusBannerSeveritySchema = z.enum(['Info', 'Warning', 'Critical']);
export type StatusBannerSeverity = z.infer<typeof StatusBannerSeveritySchema>;

export const PublicStatusBannerResponseSchema = z.object({
  messageId: z.string(),
  message: z.string(),
  severity: StatusBannerSeveritySchema,
  updatedAt: z.string(),
});
export type PublicStatusBannerResponse = z.infer<typeof PublicStatusBannerResponseSchema>;

export const AdminStatusBannerResponseSchema = z.object({
  message: z.string(),
  severity: StatusBannerSeveritySchema,
  isActive: z.boolean(),
  startsAt: z.string().nullable(),
  endsAt: z.string().nullable(),
  updatedAt: z.string(),
  updatedBy: z.string().nullable(),
});
export type AdminStatusBannerResponse = z.infer<typeof AdminStatusBannerResponseSchema>;

export const UpdateStatusBannerCommandSchema = z.object({
  message: z.string().min(1).max(500),
  severity: StatusBannerSeveritySchema,
  isActive: z.boolean(),
  startsAt: z.string().nullable(),
  endsAt: z.string().nullable(),
});
export type UpdateStatusBannerCommand = z.infer<typeof UpdateStatusBannerCommandSchema>;
