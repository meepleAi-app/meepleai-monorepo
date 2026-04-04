/**
 * Admin Content Management Schemas
 *
 * Publication workflow, prompt templates, versions, audit logs,
 * N8N workflow templates, and game bulk import.
 */

import { z } from 'zod';

// ========== Publication Workflow (Issue #3480 + #3481) ==========

/**
 * Approval status for game publication workflow (matches C# ApprovalStatus enum)
 * Issue #3481: Backend publication workflow
 */
export const ApprovalStatusSchema = z.enum(['Draft', 'PendingReview', 'Approved', 'Rejected']);
export type ApprovalStatus = z.infer<typeof ApprovalStatusSchema>;

/**
 * Request to publish game to SharedGameCatalog
 * Issue #3480: Frontend wizard integration
 */
export const PublishGameRequestSchema = z.object({
  status: ApprovalStatusSchema,
});
export type PublishGameRequest = z.infer<typeof PublishGameRequestSchema>;

/**
 * Response from publish game endpoint
 */
export const PublishGameResponseSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  isPublished: z.boolean(),
  approvalStatus: ApprovalStatusSchema,
  publishedAt: z.string().datetime().nullable(),
});
export type PublishGameResponse = z.infer<typeof PublishGameResponseSchema>;

// ========== Prompt Template Management ==========

export const PromptTemplateSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  category: z.string().min(1),
  description: z.string().nullable().optional(),
  isActive: z.boolean(),
  activeVersionId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
});

export type PromptTemplate = z.infer<typeof PromptTemplateSchema>;

export const CreatePromptRequestSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  content: z.string().min(1),
  isActive: z.boolean().optional(),
});

export type CreatePromptRequest = z.infer<typeof CreatePromptRequestSchema>;

export const UpdatePromptRequestSchema = z.object({
  name: z.string().min(1).optional(),
  description: z.string().optional(),
  content: z.string().min(1).optional(),
  isActive: z.boolean().optional(),
});

export type UpdatePromptRequest = z.infer<typeof UpdatePromptRequestSchema>;

export const PromptResponseSchema = z.object({
  template: PromptTemplateSchema,
  message: z.string().optional(),
});

export type PromptResponse = z.infer<typeof PromptResponseSchema>;

export const DeletePromptResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type DeletePromptResponse = z.infer<typeof DeletePromptResponseSchema>;

// ========== Prompt Versions & Audit ==========

export const PromptVersionSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  versionNumber: z.number(),
  content: z.string(),
  isActive: z.boolean(),
  createdAt: z.string().datetime(),
  createdBy: z.string().uuid().nullable().optional(),
  activatedAt: z.string().datetime().nullable().optional(),
});

export type PromptVersion = z.infer<typeof PromptVersionSchema>;

export const PromptVersionsResponseSchema = z.object({
  versions: z.array(PromptVersionSchema),
});

export type PromptVersionsResponse = z.infer<typeof PromptVersionsResponseSchema>;

export const PromptAuditLogSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  versionNumber: z.number(),
  action: z.string(),
  performedBy: z.string().uuid().nullable().optional(),
  performedAt: z.string().datetime(),
  details: z.string().nullable().optional(),
});

export type PromptAuditLog = z.infer<typeof PromptAuditLogSchema>;

export const PromptAuditLogsResponseSchema = z.object({
  logs: z.array(PromptAuditLogSchema),
  totalPages: z.number(),
});

export type PromptAuditLogsResponse = z.infer<typeof PromptAuditLogsResponseSchema>;

export const CreatePromptVersionRequestSchema = z.object({
  content: z.string().min(1),
  isActive: z.boolean().optional(),
});

export type CreatePromptVersionRequest = z.infer<typeof CreatePromptVersionRequestSchema>;

export const CreatePromptVersionResponseSchema = z.object({
  id: z.string().uuid(),
  versionNumber: z.number(),
  message: z.string().optional(),
});

export type CreatePromptVersionResponse = z.infer<typeof CreatePromptVersionResponseSchema>;

// ========== N8N Workflow Templates ==========

export const TemplateParameterSchema = z.object({
  name: z.string(),
  label: z.string(),
  type: z.enum(['string', 'number', 'boolean', 'select']),
  required: z.boolean(),
  defaultValue: z.string().optional(),
  description: z.string().optional(),
  options: z.array(z.string()).optional(),
  sensitive: z.boolean(),
});

export type TemplateParameter = z.infer<typeof TemplateParameterSchema>;

export const WorkflowTemplateSchema = z.object({
  id: z.string(),
  name: z.string(),
  version: z.string(),
  category: z.string(),
  description: z.string(),
  icon: z.string().optional(),
  tags: z.array(z.string()),
  parameters: z.array(TemplateParameterSchema),
});

export type WorkflowTemplate = z.infer<typeof WorkflowTemplateSchema>;

export const WorkflowTemplateDetailSchema = WorkflowTemplateSchema.extend({
  workflow: z.any(), // N8N workflow JSON structure
});

export type WorkflowTemplateDetail = z.infer<typeof WorkflowTemplateDetailSchema>;

export const ImportWorkflowResponseSchema = z.object({
  workflowId: z.string(),
  message: z.string(),
});

export type ImportWorkflowResponse = z.infer<typeof ImportWorkflowResponseSchema>;

// ========== Game Bulk Import (Issue #4355) ==========

export const BulkImportErrorSchema = z.object({
  bggId: z.number().nullable().optional(),
  gameName: z.string().nullable().optional(),
  reason: z.string(),
  errorType: z.string(),
});
export type BulkImportError = z.infer<typeof BulkImportErrorSchema>;

export const BulkImportFromJsonResultSchema = z.object({
  total: z.number().int().nonnegative(),
  enqueued: z.number().int().nonnegative(),
  skipped: z.number().int().nonnegative(),
  failed: z.number().int().nonnegative(),
  errors: z.array(BulkImportErrorSchema),
});
export type BulkImportFromJsonResult = z.infer<typeof BulkImportFromJsonResultSchema>;
