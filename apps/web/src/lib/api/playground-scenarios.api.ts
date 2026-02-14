/**
 * Playground Test Scenarios API Client
 * Issue #4391 - Test Scenarios UI CRUD
 * Issue #4396 - Backend entity (separate PR)
 */
import { HttpClient } from './core/httpClient';

import type {
  PlaygroundTestScenarioDto,
  CreatePlaygroundTestScenario,
  UpdatePlaygroundTestScenario,
} from './schemas/playground-scenarios.schemas';

const httpClient = new HttpClient({});
const BASE_URL = '/api/v1/admin/playground/scenarios';

export const playgroundScenariosApi = {
  async getAll(params?: {
    agentDefinitionId?: string;
    tag?: string;
  }): Promise<PlaygroundTestScenarioDto[]> {
    const searchParams = new URLSearchParams();
    if (params?.agentDefinitionId) searchParams.set('agentDefinitionId', params.agentDefinitionId);
    if (params?.tag) searchParams.set('tag', params.tag);

    const query = searchParams.toString();
    const url = query ? `${BASE_URL}?${query}` : BASE_URL;

    const result = await httpClient.get<PlaygroundTestScenarioDto[]>(url);
    return result ?? [];
  },

  async getById(id: string): Promise<PlaygroundTestScenarioDto> {
    const result = await httpClient.get<PlaygroundTestScenarioDto>(`${BASE_URL}/${id}`);
    if (!result) throw new Error(`Scenario ${id} not found`);
    return result;
  },

  async create(data: CreatePlaygroundTestScenario): Promise<PlaygroundTestScenarioDto> {
    const result = await httpClient.post<PlaygroundTestScenarioDto>(BASE_URL, data);
    if (!result) throw new Error('Failed to create scenario');
    return result;
  },

  async update(id: string, data: UpdatePlaygroundTestScenario): Promise<PlaygroundTestScenarioDto> {
    const result = await httpClient.put<PlaygroundTestScenarioDto>(`${BASE_URL}/${id}`, data);
    if (!result) throw new Error(`Failed to update scenario ${id}`);
    return result;
  },

  async delete(id: string): Promise<void> {
    await httpClient.delete(`${BASE_URL}/${id}`);
  },
};
