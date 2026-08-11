/**
 * useBudget — Issue #1838 SP5 F4-C5
 *
 * Query + upsert mutation for the singleton AppBudget. Returns `null` when
 * the budget has never been configured (FE shows empty-state CTA).
 *
 * 409 ConflictException on stale `xmin` is surfaced via React Query's
 * error path — callers should refetch and retry.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { budgetApi } from '@/lib/api/budget.api';
import type {
  AppBudget,
  UpsertAppBudgetRequest,
  UpsertAppBudgetResult,
} from '@/lib/api/schemas/budget.schemas';

export const APP_BUDGET_QUERY_KEY = ['admin', 'budget'] as const;

export function useBudget() {
  const queryClient = useQueryClient();

  const budgetQuery = useQuery<AppBudget | null>({
    queryKey: APP_BUDGET_QUERY_KEY,
    queryFn: () => budgetApi.get(),
    // Spend KPIs change on every ledger entry; 30s is the standard interval
    // for admin live dashboards in this codebase (matches AlertsTab pattern).
    refetchInterval: 30_000,
    retry: 1,
  });

  const upsertMutation = useMutation<UpsertAppBudgetResult, Error, UpsertAppBudgetRequest>({
    mutationFn: body => budgetApi.upsert(body),
    onSuccess: () => {
      // Bubble through to BudgetKpiStrip + BudgetGauge consumers.
      void queryClient.invalidateQueries({ queryKey: APP_BUDGET_QUERY_KEY });
    },
  });

  return {
    budget: budgetQuery.data ?? null,
    isLoading: budgetQuery.isLoading,
    isError: budgetQuery.isError,
    error: budgetQuery.error,
    refetch: budgetQuery.refetch,
    upsert: upsertMutation.mutateAsync,
    isUpserting: upsertMutation.isPending,
    upsertError: upsertMutation.error,
  };
}
