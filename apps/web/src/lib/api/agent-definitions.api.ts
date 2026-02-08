import { httpClient } from './http-client';
import type {
  AgentDefinitionDto,
  CreateAgentDefinition,
  UpdateAgentDefinition,
} from './schemas/agent-definitions.schemas';

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

    return httpClient.get<AgentDefinitionDto[]>(url);
  },

  /**
   * Get agent definition by ID
   */
  async getById(id: string): Promise<AgentDefinitionDto> {
    return httpClient.get<AgentDefinitionDto>(`${BASE_URL}/${id}`);
  },

  /**
   * Create new agent definition
   */
  async create(data: CreateAgentDefinition): Promise<AgentDefinitionDto> {
    return httpClient.post<AgentDefinitionDto>(BASE_URL, data);
  },

  /**
   * Update existing agent definition
   */
  async update(id: string, data: Omit<UpdateAgentDefinition, 'id'>): Promise<AgentDefinitionDto> {
    return httpClient.put<AgentDefinitionDto>(`${BASE_URL}/${id}`, data);
  },

  /**
   * Delete agent definition
   */
  async delete(id: string): Promise<void> {
    return httpClient.delete<void>(`${BASE_URL}/${id}`);
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
