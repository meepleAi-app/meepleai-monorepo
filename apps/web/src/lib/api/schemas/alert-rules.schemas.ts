import { z } from 'zod';

export const alertRuleSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  alertType: z.string().min(1),
  severity: z.enum(['Info', 'Warning', 'Error', 'Critical']),
  thresholdValue: z.number().min(0),
  thresholdUnit: z.string().min(1),
  durationMinutes: z.number().int().min(1),
  isEnabled: z.boolean(),
  description: z.string().optional().nullable(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export const createAlertRuleSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  alertType: z.string().min(1, 'Alert type is required'),
  severity: z.enum(['Info', 'Warning', 'Error', 'Critical']),
  thresholdValue: z.number().min(0, 'Threshold must be positive'),
  thresholdUnit: z.string().min(1, 'Unit is required'),
  durationMinutes: z.number().int().min(1, 'Duration must be at least 1 minute'),
  description: z.string().optional(),
});

export const updateAlertRuleSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  severity: z.enum(['Info', 'Warning', 'Error', 'Critical']),
  thresholdValue: z.number().min(0, 'Threshold must be positive'),
  thresholdUnit: z.string().min(1, 'Unit is required'),
  durationMinutes: z.number().int().min(1, 'Duration must be at least 1 minute'),
  description: z.string().optional(),
});

export const alertTemplateSchema = z.object({
  name: z.string(),
  alertType: z.string(),
  severity: z.string(),
  thresholdValue: z.number(),
  thresholdUnit: z.string(),
  durationMinutes: z.number(),
  description: z.string(),
  category: z.string(),
});

export type AlertRule = z.infer<typeof alertRuleSchema>;
export type CreateAlertRule = z.infer<typeof createAlertRuleSchema>;
export type UpdateAlertRule = z.infer<typeof updateAlertRuleSchema>;
export type AlertTemplate = z.infer<typeof alertTemplateSchema>;
