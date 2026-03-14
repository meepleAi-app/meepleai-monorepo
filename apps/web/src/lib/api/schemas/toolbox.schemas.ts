/**
 * Toolbox Schemas
 *
 * Zod schemas and TypeScript types for the GameToolbox bounded context.
 * Epic #412 — Game Toolbox.
 */

import { z } from 'zod';

// ============================================================================
// Value Objects
// ============================================================================

export const PlayerInfoSchema = z.object({
  name: z.string(),
  color: z.string(),
  avatarUrl: z.string().nullable().optional(),
});

export type PlayerInfo = z.infer<typeof PlayerInfoSchema>;

export const SharedContextSchema = z.object({
  players: z.array(PlayerInfoSchema),
  currentPlayerIndex: z.number().int(),
  currentRound: z.number().int(),
  customProperties: z.record(z.string(), z.string()),
});

export type SharedContext = z.infer<typeof SharedContextSchema>;

// ============================================================================
// Tool DTOs
// ============================================================================

export const ToolboxToolSchema = z.object({
  id: z.string().uuid(),
  type: z.string(),
  config: z.string().default('{}'),
  state: z.string().default('{}'),
  isEnabled: z.boolean(),
  order: z.number().int(),
});

export type ToolboxToolDto = z.infer<typeof ToolboxToolSchema>;

// ============================================================================
// Phase DTOs
// ============================================================================

export const PhaseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  order: z.number().int(),
  activeToolIds: z.array(z.string().uuid()),
});

export type PhaseDto = z.infer<typeof PhaseSchema>;

// ============================================================================
// Toolbox DTO
// ============================================================================

export const ToolboxSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameId: z.string().uuid().nullable(),
  mode: z.enum(['Freeform', 'Phased']),
  sharedContext: SharedContextSchema,
  currentPhaseId: z.string().uuid().nullable(),
  tools: z.array(ToolboxToolSchema),
  phases: z.array(PhaseSchema),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type ToolboxDto = z.infer<typeof ToolboxSchema>;

// ============================================================================
// Card Draw
// ============================================================================

export const DrawnCardSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  value: z.string().nullable(),
  suit: z.string().nullable(),
  customProperties: z.record(z.string(), z.string()),
});

export type DrawnCardDto = z.infer<typeof DrawnCardSchema>;

export const CardDrawResultSchema = z.object({
  cards: z.array(DrawnCardSchema),
  remainingInDeck: z.number().int(),
});

export type CardDrawResultDto = z.infer<typeof CardDrawResultSchema>;

// ============================================================================
// Template DTOs
// ============================================================================

export const ToolboxTemplateSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  gameId: z.string().uuid().nullable(),
  mode: z.string(),
  source: z.string(),
  toolsJson: z.string(),
  phasesJson: z.string(),
  createdAt: z.string().datetime(),
});

export type ToolboxTemplateDto = z.infer<typeof ToolboxTemplateSchema>;

// ============================================================================
// Available Tools
// ============================================================================

export const AvailableToolSchema = z.object({
  type: z.string(),
  displayName: z.string(),
  category: z.string(),
});

export type AvailableToolDto = z.infer<typeof AvailableToolSchema>;

// ============================================================================
// Request Types
// ============================================================================

export interface CreateToolboxRequest {
  name: string;
  gameId?: string;
  mode?: 'Freeform' | 'Phased';
}

export interface AddToolRequest {
  type: string;
  config?: string;
}

export interface UpdateSharedContextRequest {
  players: PlayerInfo[];
  currentPlayerIndex?: number;
  currentRound?: number;
  customProperties?: Record<string, string>;
}

export interface AddPhaseRequest {
  name: string;
  activeToolIds?: string[];
}

export interface CreateCardDeckRequest {
  name: string;
  deckType?: 'Standard52' | 'Standard52WithJokers' | 'Custom';
  customCards?: Array<{
    name: string;
    value?: string;
    suit?: string;
    customProperties?: Record<string, string>;
  }>;
}
