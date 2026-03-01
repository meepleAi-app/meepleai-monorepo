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
