import { HttpClient } from './core/httpClient';

import type {
  AgentCostEstimationResult,
  EstimateAgentCostRequest,
} from './schemas/cost-calculator.schemas';

const api = new HttpClient();

/**
 * Issue #1838 SP5 F4-C5 — Cost Simulator estimate endpoint client.
 *
 * Backend: `apps/api/src/Api/Routing/CostCalculatorEndpoints.cs` (issue #3725).
 * Auth: admin-only.
 *
 * Only the estimate sub-route is wrapped here — the scenario save/load flows
 * stay out-of-scope for #1838 and will be wired in a follow-up.
 */
export const costCalculatorApi = {
  estimate: async (body: EstimateAgentCostRequest): Promise<AgentCostEstimationResult> => {
    const result = await api.post<AgentCostEstimationResult>(
      '/api/v1/admin/cost-calculator/estimate',
      body
    );
    if (!result) throw new Error('Cost estimate returned empty payload');
    return result;
  },
};
