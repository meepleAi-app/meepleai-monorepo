/**
 * Agent Configuration Schemas
 *
 * Backend DTOs for the LLM configuration of an AgentDefinition (admin).
 *
 * Issue #4138: this file also held the per-game agent customization of the user
 * library (Issue #2518: model, personality, detail level, notes for
 * /api/v1/library/games/{gameId}/agent-config). No answer ever read that config,
 * and with one system agent (ADR-094) it is retired.
 */

// ============================================================================
// Backend Model DTOs (from GET /api/v1/models and PATCH /api/v1/agents/:id/configuration)
// ============================================================================

/** ModelDto from GET /api/v1/models?tier= */
export interface BackendModelDto {
  id: string;
  name: string;
  provider: string;
  tier: string;
  costPer1kInputTokens: number;
  costPer1kOutputTokens: number;
  maxTokens: number;
  supportsStreaming: boolean;
  description?: string;
}

/** Response from GET /api/v1/models */
export interface GetModelsResponse {
  models: BackendModelDto[];
}

/**
 * AgentConfigurationDto from PATCH/GET /api/v1/agents/:id/configuration.
 *
 * LLM configuration only (model/provider/temperature/maxTokens). Per-agent KB linking
 * (selected documents) is NOT part of this contract — it is a separate flow
 * (see useGameAgentDocuments). Issue #3394 removed the previously
 * accepted-and-discarded `selectedDocumentIds` field.
 */
export interface BackendAgentConfigurationDto {
  id: string;
  agentId: string;
  llmModel: string;
  llmProvider: string;
  temperature: number;
  maxTokens: number;
  isCurrent: boolean;
  createdAt: string;
}

/**
 * Request body for PATCH /api/v1/agents/:id/configuration.
 * LLM config only — see BackendAgentConfigurationDto note re: KB linking (#3394).
 */
export interface UpdateAgentConfigurationRequest {
  modelId?: string;
  temperature?: number;
  maxTokens?: number;
}
