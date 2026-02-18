/**
 * Budget API Client
 * Handles user credit budgets and admin budget overview
 */

export interface UserBudgetDto {
  creditsRemaining: number;
  dailyLimit: number;
  weeklyLimit: number;
  dailyResetIn: string;
  weeklyResetIn: string;
  isBlocked: boolean;
  dailyCreditsUsed: number;
  weeklyCreditsUsed: number;
}

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
 * Get user's credit budget status
 * Self-access only: users can only view their own budget
 */
export async function getUserBudget(userId: string): Promise<UserBudgetDto> {
  const response = await fetch(`/api/v1/budget/user/${userId}`, {
    method: 'GET',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch user budget: ${response.statusText}`);
  }

  return response.json();
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
