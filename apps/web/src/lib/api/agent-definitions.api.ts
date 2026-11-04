import { HttpClient } from './core/httpClient';

import type {
  AgentDefinitionDto,
  CreateAgentDefinition,
  UpdateAgentDefinition,
} from './schemas/agent-definitions.schemas';

const httpClient = new HttpClient({});
const BASE_URL = '/api/v1/admin/agent-definitions';

/**
 * 🔴 #4059 — rifiuta un id inutilizzabile PRIMA di interpolarlo nel path.
 *
 * Il difetto che questa guardia rende impossibile: la pagina di edit leggeva `params.id` da una
 * Promise, otteneva `undefined`, e il valore finiva nel path — misurato con un id reale nella
 * URL del browser: `404 /api/v1/admin/agent-definitions/undefined`. Nessuno dei due strati
 * protestava: l'interpolazione accetta qualunque cosa, e il 404 che tornava era
 * indistinguibile da «questo id non esiste», tanto che la pagina mostrava «Agent not found».
 *
 * La guardia serve proprio per quella indistinguibilita': un 404 e' un esito legittimo
 * dell'API, un id assente e' un difetto del chiamante, e i due vanno separati **prima** della
 * richiesta. Copre anche le stringhe letterali `"undefined"` e `"null"`, che sono quello che si
 * ottiene interpolando quei valori — non valori assenti ma stringhe, quindi un controllo di
 * verita' da solo non le vedrebbe.
 *
 * ⚠️ Volutamente locale a questo client, non in `HttpClient`: la versione generale cambierebbe
 * la superficie d'errore di ogni chiamata dell'applicazione (da 404 a eccezione) e va misurata
 * contro gli E2E, non introdotta di passaggio.
 */
function requireId(id: string, operation: string): string {
  const trimmed = typeof id === 'string' ? id.trim() : '';
  if (trimmed.length === 0 || trimmed === 'undefined' || trimmed === 'null') {
    throw new Error(
      `agentDefinitionsApi.${operation}: id non utilizzabile (${JSON.stringify(id)}). ` +
        'Il chiamante non ha risolto il parametro di rotta — in un Client Component `params` e' +
        ' una Promise e va letta con `use(params)`.'
    );
  }
  return trimmed;
}

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
    const safeId = requireId(id, 'getById');
    const result = await httpClient.get<AgentDefinitionDto>(`${BASE_URL}/${safeId}`);
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
    const safeId = requireId(id, 'update');
    const result = await httpClient.put<AgentDefinitionDto>(`${BASE_URL}/${safeId}`, data);
    if (!result) throw new Error(`Failed to update agent definition ${id}`);
    return result;
  },

  /**
   * Delete agent definition
   */
  async delete(id: string): Promise<void> {
    const safeId = requireId(id, 'delete');
    await httpClient.delete(`${BASE_URL}/${safeId}`);
  },

  /**
   * Toggle agent definition active status
   */
  async toggleActive(id: string): Promise<AgentDefinitionDto> {
    const safeId = requireId(id, 'toggleActive');
    const current = await this.getById(safeId);
    const updateData = {
      name: current.name,
      description: current.description,
      model: current.config.model,
      maxTokens: current.config.maxTokens,
      temperature: current.config.temperature,
      prompts: current.prompts,
      tools: current.tools,
      kbCardIds: current.kbCardIds,
      chatLanguage: current.chatLanguage ?? 'auto',
    };
    return this.update(safeId, updateData);
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
   * Start testing an agent definition (Draft → Testing)
   */
  async startTesting(id: string): Promise<void> {
    const safeId = requireId(id, 'startTesting');
    await httpClient.post(`${BASE_URL}/${safeId}/start-testing`, {});
  },

  /**
   * Publish an agent definition (Testing → Published)
   */
  async publish(id: string): Promise<void> {
    const safeId = requireId(id, 'publish');
    await httpClient.post(`${BASE_URL}/${safeId}/publish`, {});
  },

  /**
   * Unpublish an agent definition (Published → Draft)
   */
  async unpublish(id: string): Promise<void> {
    const safeId = requireId(id, 'unpublish');
    await httpClient.post(`${BASE_URL}/${safeId}/unpublish`, {});
  },

  /**
   * Clone an agent definition
   * Issue #3713: Agent Catalog actions
   */
  async clone(id: string): Promise<AgentDefinitionDto> {
    const safeId = requireId(id, 'clone');
    const source = await this.getById(safeId);
    return this.create({
      name: `${source.name} (Copy)`,
      description: source.description,
      model: source.config.model,
      maxTokens: source.config.maxTokens,
      temperature: source.config.temperature,
      prompts: source.prompts,
      tools: source.tools,
      kbCardIds: source.kbCardIds,
      chatLanguage: source.chatLanguage ?? 'auto',
    });
  },
};
