import { HttpClient } from './core/httpClient';

import type {
  AgentDefinitionDto,
  CreateAgentDefinition,
  UpdateAgentDefinition,
} from './schemas/agent-definitions.schemas';

const httpClient = new HttpClient({});
const BASE_URL = '/api/v1/admin/agent-definitions';

// ============================================================================
// Agent Catalog Stats Types (Issue #3713)
// ============================================================================

export interface AgentCatalogTimeSeriesPoint {
  date: string;
  executions: number;
  totalTokens: number;
  cost: number;
  avgLatencyMs: number;
  successRate: number;
}

export interface AgentCatalogAgentStats {
  agentDefinitionId: string;
  name: string;
  description: string | null;
  type: string;
  isActive: boolean;
  model: string | null;
  provider: string | null;
  executionCount: number;
  totalTokens: number;
  avgTokens: number;
  totalCost: number;
  successRate: number;
  avgLatencyMs: number;
  avgConfidence: number;
  lastExecutedAt: string | null;
  timeSeries: AgentCatalogTimeSeriesPoint[];
}

export interface AgentCatalogGlobalStats {
  totalExecutions: number;
  totalCost: number;
  avgSuccessRate: number;
  avgLatencyMs: number;
  avgConfidence: number;
  totalAgents: number;
  activeAgents: number;
}

export interface AgentCatalogStatsResult {
  global: AgentCatalogGlobalStats;
  agents: AgentCatalogAgentStats[];
}

export const agentDefinitionsApi = {
  /**
   * Get all agent definitions
   */
  async getAll(params?: { activeOnly?: boolean; search?: string }): Promise<AgentDefinitionDto[]> {
    const searchParams = new URLSearchParams();
    if (params?.activeOnly) searchParams.set('activeOnly', 'true');
    if (params?.search) searchParams.set('search', params.search);

    const query = searchParams.toString();
    const url = query ? `${BASE_URL}?${query}` : BASE_URL;

    const result = await httpClient.get<AgentDefinitionDto[]>(url);
    return result ?? [];
  },

  /**
   * Get agent definition by ID
   */
  async getById(id: string): Promise<AgentDefinitionDto> {
    const result = await httpClient.get<AgentDefinitionDto>(`${BASE_URL}/${id}`);
    if (!result) throw new Error(`Agent definition ${id} not found`);
    return result;
  },

  /**
   * Create new agent definition
   */
  async create(data: CreateAgentDefinition): Promise<AgentDefinitionDto> {
    const result = await httpClient.post<AgentDefinitionDto>(BASE_URL, data);
    if (!result) throw new Error('Failed to create agent definition');
    return result;
  },

  /**
   * Update existing agent definition
   */
  async update(id: string, data: Omit<UpdateAgentDefinition, 'id'>): Promise<AgentDefinitionDto> {
    const result = await httpClient.put<AgentDefinitionDto>(`${BASE_URL}/${id}`, data);
    if (!result) throw new Error(`Failed to update agent definition ${id}`);
    return result;
  },

  /**
   * Delete agent definition
   */
  async delete(id: string): Promise<void> {
    await httpClient.delete(`${BASE_URL}/${id}`);
  },

  /**
   * Toggle agent definition active status
   */
  async toggleActive(id: string): Promise<AgentDefinitionDto> {
    const current = await this.getById(id);
    const updateData = {
      name: current.name,
      description: current.description,
      model: current.config.model,
      maxTokens: current.config.maxTokens,
      temperature: current.config.temperature,
      prompts: current.prompts,
      tools: current.tools,
    };
    return this.update(id, updateData);
  },

  /**
   * Get agent catalog usage statistics
   * Issue #3713: Agent Catalog and Usage Stats
   */
  async getCatalogStats(params?: {
    range?: string;
    agentDefinitionId?: string;
  }): Promise<AgentCatalogStatsResult> {
    const searchParams = new URLSearchParams();
    if (params?.range) searchParams.set('range', params.range);
    if (params?.agentDefinitionId) searchParams.set('agentDefinitionId', params.agentDefinitionId);

    const query = searchParams.toString();
    const url = query ? `${BASE_URL}/catalog-stats?${query}` : `${BASE_URL}/catalog-stats`;

    const result = await httpClient.get<AgentCatalogStatsResult>(url);
    if (!result) throw new Error('Failed to fetch agent catalog stats');
    return result;
  },

  /**
   * Clone an agent definition
   * Issue #3713: Agent Catalog actions
   */
  async clone(id: string): Promise<AgentDefinitionDto> {
    const source = await this.getById(id);
    return this.create({
      name: `${source.name} (Copy)`,
      description: source.description,
      model: source.config.model,
      maxTokens: source.config.maxTokens,
      temperature: source.config.temperature,
      prompts: source.prompts,
      tools: source.tools,
    });
  },
};