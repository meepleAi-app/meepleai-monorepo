/**
 * Agent Typologies API — compatibility shim
 *
 * Typologies were collapsed into AgentDefinition. This module proxies
 * calls to agentDefinitionsApi so existing call sites keep compiling.
 */

import { agentDefinitionsApi } from './agent-definitions.api';

import type { AgentDefinitionDto as Typology } from './schemas/agent-definitions.schemas';

export { Typology };

export const agentTypologiesApi = {
  getAll: () => agentDefinitionsApi.getAll({ activeOnly: false }),
  getById: (id: string) => agentDefinitionsApi.getById(id),
  create: (data: Parameters<typeof agentDefinitionsApi.create>[0]) =>
    agentDefinitionsApi.create(data).then(d => ({ id: d.id })),
  update: (id: string, data: Parameters<typeof agentDefinitionsApi.update>[1]) =>
    agentDefinitionsApi.update(id, data).then(() => undefined as void),
  delete: (id: string) => agentDefinitionsApi.delete(id),
  toggle: (id: string) => agentDefinitionsApi.toggleActive(id).then(() => undefined as void),

  // Proposal methods — concept removed; kept as no-ops to avoid compile errors
  getMyProposals: (): Promise<Typology[]> => Promise.resolve([]),
  propose: (_data: Parameters<typeof agentDefinitionsApi.create>[0]): Promise<{ id: string }> =>
    Promise.reject(new Error('Proposal workflow removed')),
  test: (
    _id: string,
    _testQuery: string
  ): Promise<{ success: boolean; response: string; confidenceScore: number }> =>
    Promise.reject(new Error('Typology test workflow removed')),
  submitForApproval: (_id: string, _typology: Typology): Promise<void> =>
    Promise.reject(new Error('Typology approval workflow removed')),
};
