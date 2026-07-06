import { HttpClient } from './core/httpClient';

import type {
  AppBudget,
  UpsertAppBudgetRequest,
  UpsertAppBudgetResult,
} from './schemas/budget.schemas';

const api = new HttpClient();

/**
 * Issue #1838 SP5 F4-C5 — AppBudget singleton CRUD.
 *
 * Backend route group: `apps/api/src/Api/Routing/AdminBudgetEndpoints.cs`.
 * Auth: admin-only (`.RequireAdminSession()` per-endpoint).
 *
 * `get()` returns null when the budget has never been configured — the FE
 * shows the empty-state CTA in that branch (spec Scenario I).
 */
export const budgetApi = {
  get: async (): Promise<AppBudget | null> => {
    return api.get<AppBudget>('/api/v1/admin/budget');
  },

  /**
   * Upsert the singleton AppBudget. Pass `xmin` from the most recent
   * GET to detect concurrent edits (409 ConflictException). Omit on first
   * creation.
   */
  upsert: async (body: UpsertAppBudgetRequest): Promise<UpsertAppBudgetResult> => {
    const result = await api.put<UpsertAppBudgetResult>('/api/v1/admin/budget', body);
    if (!result) throw new Error('Upsert AppBudget returned no payload');
    return result;
  },
};
