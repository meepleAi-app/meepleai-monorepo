/**
 * Email Templates Schemas (Issue #52-#56)
 *
 * Zod validation schemas for admin email template management.
 */

import { z } from 'zod';

// ─── DTOs ────────────────────────────────────────────────────────────────────

export const EmailTemplateDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  locale: z.string(),
  subject: z.string(),
  htmlBody: z.string(),
  version: z.number().int(),
  isActive: z.boolean(),
  lastModifiedBy: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
});
export type EmailTemplateDto = z.infer<typeof EmailTemplateDtoSchema>;

export const GetEmailTemplatesResponseSchema = z.array(EmailTemplateDtoSchema);
export type GetEmailTemplatesResponse = z.infer<typeof GetEmailTemplatesResponseSchema>;

// ─── Input Schemas ───────────────────────────────────────────────────────────

export const CreateEmailTemplateInputSchema = z.object({
  name: z.string().min(1).max(100),
  locale: z.string().min(2).max(10),
  subject: z.string().min(1).max(500),
  htmlBody: z.string().min(1),
});
export type CreateEmailTemplateInput = z.infer<typeof CreateEmailTemplateInputSchema>;

export const UpdateEmailTemplateInputSchema = z.object({
  subject: z.string().min(1).max(500),
  htmlBody: z.string().min(1),
});
export type UpdateEmailTemplateInput = z.infer<typeof UpdateEmailTemplateInputSchema>;

export const PreviewEmailTemplateInputSchema = z.object({
  testData: z.record(z.string(), z.string()).optional(),
});
export type PreviewEmailTemplateInput = z.infer<typeof PreviewEmailTemplateInputSchema>;
