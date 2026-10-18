/**
 * Budget API Client
 * Admin budget overview. Issue #4212: the per-user credit budget (GET /budget/user/{id}) is
 * retired with the token-tier system it read.
 */

export interface AdminBudgetOverviewDto {
  openRouterBalanceEuros: number;
  dailySpendUsd: number;
  weeklySpendUsd: number;
  dailyBudgetUsd: number;
  weeklyBudgetUsd: number;
  dailyUsagePercent: number;
  weeklyUsagePercent: number;
}

/**
 * Get admin budget overview (OpenRouter + App budgets)
 * Admin-only access
 */
export async function getAdminBudgetOverview(): Promise<AdminBudgetOverviewDto> {
  const response = await fetch('/api/v1/admin/budget/overview', {
    method: 'GET',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch admin budget: ${response.statusText}`);
  }

  return response.json();
}
