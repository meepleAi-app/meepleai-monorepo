import { HttpClient } from './core/httpClient';

import type {
  AgentDefinitionDto,
  CreateAgentDefinition,
  UpdateAgentDefinition,
} from './schemas/agent-definitions.schemas';

const httpClient = new HttpClient({});
const BASE_URL = '/api/v1/admin/agent-definitions';

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
};