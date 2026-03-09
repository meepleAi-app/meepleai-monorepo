/**
 * Toolkit Dashboard Schemas
 *
 * Zod schemas and TypeScript types for the GameToolkit bounded context.
 * Issue #5128 — Epic B.
 */

import { z } from 'zod';

// ============================================================================
// Widget type enum
// ============================================================================

export const WidgetTypeSchema = z.enum([
  'RandomGenerator',
  'TurnManager',
  'ScoreTracker',
  'ResourceManager',
  'NoteManager',
  'Whiteboard',
]);

export type WidgetType = z.infer<typeof WidgetTypeSchema>;

// ============================================================================
// Toolkit Dashboard DTO
// ============================================================================

export const ToolkitWidgetDtoSchema = z.object({
  id: z.string().uuid(),
  type: WidgetTypeSchema,
  isEnabled: z.boolean(),
  displayOrder: z.number().int(),
  config: z.string().default('{}'),
});

export type ToolkitWidgetDto = z.infer<typeof ToolkitWidgetDtoSchema>;

export const ToolkitDashboardDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  ownerUserId: z.string().uuid().nullable(),
  isDefault: z.boolean(),
  displayName: z.string().nullable(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  widgets: z.array(ToolkitWidgetDtoSchema),
});

export type ToolkitDashboardDto = z.infer<typeof ToolkitDashboardDtoSchema>;

// ============================================================================
// Toolkit Session State DTO
// ============================================================================

export const ToolkitSessionStateDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  toolkitId: z.string().uuid(),
  widgetStates: z.record(z.string(), z.string()),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type ToolkitSessionStateDto = z.infer<typeof ToolkitSessionStateDtoSchema>;

// ============================================================================
// Request types
// ============================================================================

export interface OverrideToolkitRequest {
  displayName?: string | null;
}

export interface UpdateWidgetRequest {
  isEnabled?: boolean | null;
  configJson?: string | null;
}

export interface UpdateWidgetStateRequest {
  stateJson: string;
}

// ============================================================================
// AI Toolkit Suggestion Schemas (Phase 0 — AI Generation)
// ============================================================================

export const AiDiceToolSuggestionSchema = z.object({
  name: z.string(),
  diceType: z.string(),
  quantity: z.number(),
  customFaces: z.array(z.string()).nullable().optional(),
  isInteractive: z.boolean(),
  color: z.string().nullable().optional(),
});

export const AiCounterToolSuggestionSchema = z.object({
  name: z.string(),
  minValue: z.number(),
  maxValue: z.number(),
  defaultValue: z.number(),
  isPerPlayer: z.boolean(),
  icon: z.string().nullable().optional(),
  color: z.string().nullable().optional(),
});

export const AiTimerToolSuggestionSchema = z.object({
  name: z.string(),
  durationSeconds: z.number(),
  timerType: z.string(),
  autoStart: z.boolean(),
  color: z.string().nullable().optional(),
  isPerPlayer: z.boolean(),
  warningThresholdSeconds: z.number().nullable().optional(),
});

export const AiScoringTemplateSuggestionSchema = z.object({
  dimensions: z.array(z.string()),
  defaultUnit: z.string(),
  scoreType: z.string(),
});

export const AiTurnTemplateSuggestionSchema = z.object({
  turnOrderType: z.string(),
  phases: z.array(z.string()),
});

export const AiOverrideSuggestionSchema = z.object({
  overridesTurnOrder: z.boolean(),
  overridesScoreboard: z.boolean(),
  overridesDiceSet: z.boolean(),
});

export const AiToolkitSuggestionSchema = z.object({
  toolkitName: z.string(),
  diceTools: z.array(AiDiceToolSuggestionSchema),
  counterTools: z.array(AiCounterToolSuggestionSchema),
  timerTools: z.array(AiTimerToolSuggestionSchema),
  scoringTemplate: AiScoringTemplateSuggestionSchema.nullable(),
  turnTemplate: AiTurnTemplateSuggestionSchema.nullable(),
  overrides: AiOverrideSuggestionSchema.nullable(),
  reasoning: z.string(),
});

export type AiToolkitSuggestion = z.infer<typeof AiToolkitSuggestionSchema>;
export type AiDiceToolSuggestion = z.infer<typeof AiDiceToolSuggestionSchema>;
export type AiCounterToolSuggestion = z.infer<typeof AiCounterToolSuggestionSchema>;
export type AiTimerToolSuggestion = z.infer<typeof AiTimerToolSuggestionSchema>;
export type AiScoringTemplateSuggestion = z.infer<typeof AiScoringTemplateSuggestionSchema>;
export type AiTurnTemplateSuggestion = z.infer<typeof AiTurnTemplateSuggestionSchema>;
export type AiOverrideSuggestion = z.infer<typeof AiOverrideSuggestionSchema>;

// ============================================================================
// Template Marketplace Schemas (Phase 1)
// ============================================================================

export const TemplateStatusSchema = z.enum(['Draft', 'PendingReview', 'Approved', 'Rejected']);
export type TemplateStatus = z.infer<typeof TemplateStatusSchema>;

export const GameToolkitTemplateDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  privateGameId: z.string().uuid().nullable(),
  name: z.string(),
  version: z.number(),
  createdByUserId: z.string().uuid(),
  isPublished: z.boolean(),
  createdAt: z.string(),
  updatedAt: z.string(),
  templateStatus: TemplateStatusSchema,
  isTemplate: z.boolean(),
  reviewNotes: z.string().nullable().optional(),
  reviewedByUserId: z.string().uuid().nullable().optional(),
  reviewedAt: z.string().nullable().optional(),
  diceTools: z.array(z.object({ name: z.string() })).default([]),
  cardTools: z.array(z.object({ name: z.string() })).default([]),
  timerTools: z.array(z.object({ name: z.string() })).default([]),
  counterTools: z.array(z.object({ name: z.string() })).default([]),
  scoringTemplate: z.unknown().nullable().optional(),
  turnTemplate: z.unknown().nullable().optional(),
  stateTemplate: z
    .object({
      name: z.string(),
      description: z.string().nullable().optional(),
      category: z.string(),
    })
    .nullable()
    .optional(),
});

export type GameToolkitTemplateDto = z.infer<typeof GameToolkitTemplateDtoSchema>;
