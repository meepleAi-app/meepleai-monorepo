/**
 * Report Schemas (Issue #920)
 *
 * Zod validation schemas for report generation and scheduling APIs
 */

import { z } from 'zod';

// ========== Enums ==========

export const ReportTemplateSchema = z.enum([
  'SystemHealth',
  'UserActivity',
  'AIUsage',
  'ContentMetrics',
]);

export const ReportFormatSchema = z.enum(['CSV', 'JSON', 'PDF']);

export const ReportExecutionStatusSchema = z.enum(['Pending', 'Running', 'Completed', 'Failed']);

// ========== Request Schemas ==========

export const GenerateReportRequestSchema = z.object({
  template: ReportTemplateSchema,
  format: ReportFormatSchema,
  parameters: z.record(z.string(), z.unknown()).optional(),
});

export const ScheduleReportRequestSchema = z.object({
  name: z.string().min(1).max(100),
  description: z.string().min(1).max(500),
  template: ReportTemplateSchema,
  format: ReportFormatSchema,
  parameters: z.record(z.string(), z.unknown()).optional(),
  scheduleExpression: z.string(), // Cron expression
  emailRecipients: z.array(z.string().email()).max(10).optional(),
});

export const UpdateReportScheduleRequestSchema = z.object({
  scheduleExpression: z.string().nullable(),
  isActive: z.boolean(),
  emailRecipients: z.array(z.string().email()).max(10).optional(),
});

// ========== Response Schemas ==========

export const ScheduledReportDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string(),
  template: ReportTemplateSchema,
  format: ReportFormatSchema,
  parameters: z.record(z.string(), z.unknown()),
  scheduleExpression: z.string().nullable(),
  isActive: z.boolean(),
  createdAt: z.string().datetime(),
  lastExecutedAt: z.string().datetime().nullable(),
  createdBy: z.string(),
  emailRecipients: z.array(z.string()),
});

export const ReportExecutionDtoSchema = z.object({
  id: z.string().uuid(),
  reportId: z.string().uuid(),
  reportName: z.string(),
  template: ReportTemplateSchema,
  status: ReportExecutionStatusSchema,
  startedAt: z.string().datetime(),
  completedAt: z.string().datetime().nullable(),
  errorMessage: z.string().nullable(),
  filePath: z.string().nullable(),
  fileSize: z.number().nullable(),
});

export const ScheduleReportResponseSchema = z.object({
  reportId: z.string().uuid(),
  message: z.string(),
});

export const GetScheduledReportsResponseSchema = z.array(ScheduledReportDtoSchema);

export const GetReportExecutionsResponseSchema = z.array(ReportExecutionDtoSchema);

// ========== Type Exports ==========

export type ReportTemplate = z.infer<typeof ReportTemplateSchema>;
export type ReportFormat = z.infer<typeof ReportFormatSchema>;
export type ReportExecutionStatus = z.infer<typeof ReportExecutionStatusSchema>;

export type GenerateReportRequest = z.infer<typeof GenerateReportRequestSchema>;
export type ScheduleReportRequest = z.infer<typeof ScheduleReportRequestSchema>;
export type UpdateReportScheduleRequest = z.infer<typeof UpdateReportScheduleRequestSchema>;

export type ScheduledReportDto = z.infer<typeof ScheduledReportDtoSchema>;
export type ReportExecutionDto = z.infer<typeof ReportExecutionDtoSchema>;
export type ScheduleReportResponse = z.infer<typeof ScheduleReportResponseSchema>;
