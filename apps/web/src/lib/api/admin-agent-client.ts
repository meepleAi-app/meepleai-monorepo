/**
 * ISSUE-3709: Admin Agent Definition API Client
 * API methods for Agent Builder UI CRUD operations
 */

import type {
  CreateAgentDefinitionRequest,
  UpdateAgentDefinitionRequest,
  AgentDefinitionResponse,
} from '../schemas/agent-definition-schema';

const BASE_URL = '/api/v1/admin/agent-definitions';

/**
 * Create a new agent definition
 */
export async function createAgentDefinition(
  request: CreateAgentDefinitionRequest
): Promise<AgentDefinitionResponse> {
  const response = await fetch(BASE_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to create agent' }));
    throw new Error(error.message || `HTTP ${response.status}: Failed to create agent`);
  }

  return response.json();
}

/**
 * Update an existing agent definition
 */
export async function updateAgentDefinition(
  id: string,
  request: UpdateAgentDefinitionRequest
): Promise<AgentDefinitionResponse> {
  const response = await fetch(`${BASE_URL}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to update agent' }));
    throw new Error(error.message || `HTTP ${response.status}: Failed to update agent`);
  }

  return response.json();
}

/**
 * Delete an agent definition
 */
export async function deleteAgentDefinition(id: string): Promise<void> {
  const response = await fetch(`${BASE_URL}/${id}`, {
    method: 'DELETE',
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to delete agent' }));
    throw new Error(error.message || `HTTP ${response.status}: Failed to delete agent`);
  }
}

/**
 * Get all agent definitions
 */
export async function getAgentDefinitions(params?: {
  activeOnly?: boolean;
  search?: string;
}): Promise<AgentDefinitionResponse[]> {
  const searchParams = new URLSearchParams();

  if (params?.activeOnly !== undefined) {
    searchParams.set('activeOnly', String(params.activeOnly));
  }

  if (params?.search) {
    searchParams.set('search', params.search);
  }

  const url = searchParams.toString() ? `${BASE_URL}?${searchParams}` : BASE_URL;

  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: Failed to fetch agent definitions`);
  }

  return response.json();
}

/**
 * Get a single agent definition by ID
 */
export async function getAgentDefinition(id: string): Promise<AgentDefinitionResponse> {
  const response = await fetch(`${BASE_URL}/${id}`);

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error(`Agent definition ${id} not found`);
    }
    throw new Error(`HTTP ${response.status}: Failed to fetch agent definition`);
  }

  return response.json();
}

/**
 * Get agent definition statistics
 */
export async function getAgentDefinitionStats(): Promise<{
  totalCount: number;
  byType: Record<string, number>;
  recentTemplates: AgentDefinitionResponse[];
}> {
  const response = await fetch(`${BASE_URL}/stats`);

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: Failed to fetch agent stats`);
  }

  return response.json();
}

/**
 * Test an agent with a sample query (playground)
 */
export async function testAgent(agentId: string, query: string): Promise<{
  response: string;
  confidence: number;
  executionTimeMs: number;
}> {
  const response = await fetch(`${BASE_URL}/${agentId}/playground/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: Failed to test agent`);
  }

  return response.json();
}
