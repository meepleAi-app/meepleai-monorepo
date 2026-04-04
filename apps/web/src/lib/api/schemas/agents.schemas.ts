/**
 * Agent Schemas (Issue #868)
 *
 * Zod schemas for KnowledgeBase Agent entities and operations.
 * Matches backend DTOs from Api.BoundedContexts.KnowledgeBase.Application.DTOs
 */

import { z } from 'zod';

/**
 * Agent DTO Schema
 * Matches AgentDto from backend
 */
export const AgentDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  type: z.string(),
  strategyName: z.string(),
  strategyParameters: z.record(z.string(), z.any()),
  isActive: z.boolean(),
  createdAt: z.string().datetime(),
  lastInvokedAt: z.string().datetime().nullable(),
  invocationCount: z.number().int().nonnegative(),
  isRecentlyUsed: z.boolean(),
  isIdle: z.boolean(),
  // Issue #4914: user-owned agent fields
  gameId: z.string().uuid().nullable().optional(),
  gameName: z.string().nullable().optional(),
  createdByUserId: z.string().uuid().nullable().optional(),
});

export type AgentDto = z.infer<typeof AgentDtoSchema>;

/**
 * Agent Response DTO Schema
 * Matches AgentResponseDto from backend
 */
export const AgentResponseDtoSchema = z.object({
  invocationId: z.string().uuid(),
  agentId: z.string().uuid(),
  agentName: z.string(),
  query: z.string(),
  response: z.string(),
  confidence: z.number(),
  resultCount: z.number().int().nonnegative(),
  processingTimeMs: z.number().int().nonnegative(),
  timestamp: z.string().datetime(),
  searchResults: z.array(
    z.object({
      documentId: z.string().uuid(),
      gameId: z.string().uuid(),
      snippet: z.string(),
      score: z.number(),
      pageNumber: z.number().int().nullable(),
      metadata: z.record(z.string(), z.any()).nullable(),
    })
  ),
  metadata: z.record(z.string(), z.any()).nullable(),
});

export type AgentResponseDto = z.infer<typeof AgentResponseDtoSchema>;

/**
 * Invoke Agent Request Schema
 * Matches InvokeAgentRequest from backend
 */
export const InvokeAgentRequestSchema = z.object({
  query: z.string().min(1, 'Query is required'),
  gameId: z.string().uuid().optional(),
  chatThreadId: z.string().uuid().optional(),
});

export type InvokeAgentRequest = z.infer<typeof InvokeAgentRequestSchema>;

/**
 * Get All Agents Response Schema
 */
export const GetAllAgentsResponseSchema = z.object({
  success: z.boolean(),
  agents: z.array(AgentDtoSchema),
  count: z.number().int().nonnegative(),
});

export type GetAllAgentsResponse = z.infer<typeof GetAllAgentsResponseSchema>;

/**
 * Create Agent Request Schema
 */
export const CreateAgentRequestSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  type: z.string().min(1, 'Type is required'),
  strategyName: z.string().min(1, 'Strategy name is required'),
  strategyParameters: z.record(z.string(), z.any()).optional(),
  isActive: z.boolean().optional(),
});

export type CreateAgentRequest = z.infer<typeof CreateAgentRequestSchema>;

/**
 * Configure Agent Request Schema
 */
export const ConfigureAgentRequestSchema = z.object({
  strategyName: z.string().min(1, 'Strategy name is required'),
  strategyParameters: z.record(z.string(), z.any()).optional(),
});

export type ConfigureAgentRequest = z.infer<typeof ConfigureAgentRequestSchema>;

/**
 * Configure Agent Response Schema
 */
export const ConfigureAgentResponseSchema = z.object({
  success: z.boolean(),
  message: z.string().optional(),
  errorCode: z.string().optional(),
});

export type ConfigureAgentResponse = z.infer<typeof ConfigureAgentResponseSchema>;

/**
 * Chess Analysis Schema (Issue #1977)
 * Matches ChessAnalysis from backend Contracts
 */
export const ChessAnalysisSchema = z.object({
  fenPosition: z.string().nullable(),
  evaluationSummary: z.string().nullable(),
  keyConsiderations: z.array(z.string()),
});

export type ChessAnalysis = z.infer<typeof ChessAnalysisSchema>;

/**
 * Snippet Schema (Issue #1977)
 * Matches Snippet from backend search results
 */
export const SnippetSchema = z.object({
  documentId: z.string().uuid(),
  gameId: z.string().uuid(),
  snippet: z.string(),
  score: z.number(),
  pageNumber: z.number().int().nullable(),
  metadata: z.record(z.string(), z.any()).nullable(),
});

export type Snippet = z.infer<typeof SnippetSchema>;

/**
 * Chess Agent Response Schema (Issue #1977)
 * Matches ChessAgentResponse from backend Contracts
 */
export const ChessAgentResponseSchema = z.object({
  answer: z.string(),
  analysis: ChessAnalysisSchema.nullable(),
  suggestedMoves: z.array(z.string()),
  sources: z.array(SnippetSchema),
  promptTokens: z.number().int().nonnegative(),
  completionTokens: z.number().int().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  confidence: z.number().nullable(),
  metadata: z.record(z.string(), z.string()).nullable(),
});

export type ChessAgentResponse = z.infer<typeof ChessAgentResponseSchema>;

/**
 * Setup Guide Response Step Schema (Issue #1977)
 * Matches SetupGuideStep from backend Contracts (non-streaming response)
 */
export const SetupGuideResponseStepSchema = z.object({
  stepNumber: z.number().int().positive(),
  title: z.string(),
  instruction: z.string(),
  references: z.array(SnippetSchema),
  isOptional: z.boolean(),
});

export type SetupGuideResponseStep = z.infer<typeof SetupGuideResponseStepSchema>;

/**
 * Setup Guide Response Schema (Issue #1977)
 * Matches SetupGuideResponse from backend Contracts
 */
export const SetupGuideResponseSchema = z.object({
  gameTitle: z.string(),
  steps: z.array(SetupGuideResponseStepSchema),
  estimatedSetupTimeMinutes: z.number().int().nonnegative(),
  promptTokens: z.number().int().nonnegative(),
  completionTokens: z.number().int().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  confidence: z.number().nullable(),
});

export type SetupGuideResponse = z.infer<typeof SetupGuideResponseSchema>;

// ========== Agent Documents (Issue #2399) ==========

/**
 * Selected Document DTO Schema
 * Matches SelectedDocumentDto from backend
 * Issue #2399: Knowledge Base Document Selection
 */
export const SelectedDocumentDtoSchema = z.object({
  id: z.string().uuid(),
  sharedGameId: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  documentType: z.number().int().min(0).max(2), // Rulebook=0, Errata=1, Homerule=2
  version: z.string(),
  isActive: z.boolean(),
  tags: z.array(z.string()),
  gameName: z.string().nullable(),
});

export type SelectedDocumentDto = z.infer<typeof SelectedDocumentDtoSchema>;

/**
 * Agent Documents DTO Schema
 * Matches AgentDocumentsDto from backend
 * Issue #2399: Knowledge Base Document Selection
 */
export const AgentDocumentsDtoSchema = z.object({
  agentId: z.string().uuid(),
  documents: z.array(SelectedDocumentDtoSchema),
});

export type AgentDocumentsDto = z.infer<typeof AgentDocumentsDtoSchema>;

/**
 * Update Agent Documents Request Schema
 * Issue #2399: Knowledge Base Document Selection
 */
export const UpdateAgentDocumentsRequestSchema = z.object({
  documentIds: z.array(z.string().uuid()).max(50),
});

export type UpdateAgentDocumentsRequest = z.infer<typeof UpdateAgentDocumentsRequestSchema>;

/**
 * Update Agent Documents Response Schema
 * Matches UpdateAgentDocumentsResult from backend
 * Issue #2399: Knowledge Base Document Selection
 */
export const UpdateAgentDocumentsResponseSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  agentId: z.string().uuid(),
  documentCount: z.number().int().nonnegative(),
  errorCode: z.string().nullable().optional(),
});

export type UpdateAgentDocumentsResponse = z.infer<typeof UpdateAgentDocumentsResponseSchema>;

// ========== Issue #4126: Chat API Schemas ==========

/**
 * Chat with Agent Request Schema
 */
export const ChatWithAgentRequestSchema = z.object({
  message: z.string().min(1, 'Message is required').max(2000, 'Message too long'),
});

export type ChatWithAgentRequest = z.infer<typeof ChatWithAgentRequestSchema>;

/**
 * SSE Streaming Event Schema
 */
export const SSEEventSchema = z.object({
  type: z.enum(['StateUpdate', 'Token', 'Complete', 'Error', 'Citations']),
  data: z.unknown(),
  timestamp: z.string().datetime(),
});

export type SSEEvent = z.infer<typeof SSEEventSchema>;

/**
 * Recent Agents Response (array of AgentDto)
 */
export const RecentAgentsResponseSchema = z.array(AgentDtoSchema);
export type RecentAgentsResponse = z.infer<typeof RecentAgentsResponseSchema>;

// ========== Player Mode AI Suggestion (Issue #2421) ==========

/**
 * Player Mode Suggestion Request Schema
 * Issue #2421: Player Mode UI Controls
 */
export const PlayerModeSuggestionRequestSchema = z.object({
  gameId: z.string().uuid(),
  gameState: z.record(z.string(), z.any()), // Flexible game state object
  query: z.string().optional(), // Optional context/question from player
  chatThreadId: z.string().uuid().optional(),
});

export type PlayerModeSuggestionRequest = z.infer<typeof PlayerModeSuggestionRequestSchema>;

/**
 * Suggested Move Schema
 * Represents a single move suggestion from AI
 */
export const SuggestedMoveSchema = z.object({
  action: z.string(), // e.g., "Place resource token on space 5"
  rationale: z.string(), // Why this move is suggested
  expectedOutcome: z.string().optional(), // What happens after this move
  confidence: z.number().min(0).max(1), // Move-specific confidence (0.0-1.0)
});

export type SuggestedMove = z.infer<typeof SuggestedMoveSchema>;

/**
 * Player Mode Suggestion Response Schema
 * Issue #2421: Player Mode UI Controls
 */
export const PlayerModeSuggestionResponseSchema = z.object({
  primarySuggestion: SuggestedMoveSchema,
  alternativeMoves: z.array(SuggestedMoveSchema).max(3).optional(),
  overallConfidence: z.number().min(0).max(1), // Overall AI confidence
  strategicContext: z.string().optional(), // High-level strategic advice
  sources: z.array(SnippetSchema).optional(), // Rule references if applicable
  promptTokens: z.number().int().nonnegative(),
  completionTokens: z.number().int().nonnegative(),
  totalTokens: z.number().int().nonnegative(),
  processingTimeMs: z.number().int().nonnegative(),
  metadata: z.record(z.string(), z.any()).nullable(),
});

export type PlayerModeSuggestionResponse = z.infer<typeof PlayerModeSuggestionResponseSchema>;
