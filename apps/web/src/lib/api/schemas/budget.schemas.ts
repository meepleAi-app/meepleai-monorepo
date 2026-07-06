import { z } from 'zod';

// Issue #1838 SP5 F4-C5 — AppBudget singleton + computed spend KPIs.
// Backend: AppBudgetDto + SpendBreakdownDto (GetAppBudgetQuery.cs).

export const spendBreakdownSchema = z.object({
  today: z.number(),
  thisMonth: z.number(),
  projectedMonthEnd: z.number(),
});

export const appBudgetSchema = z.object({
  id: z.string().uuid(),
  monthlyLimit: z.number(),
  currency: z.string(),
  alertThresholdPct: z.number().int().min(1).max(99),
  criticalThresholdPct: z.number().int().min(1).max(100),
  isEnabled: z.boolean(),
  spent: spendBreakdownSchema,
  daysRemaining: z.number().int(),
  updatedAt: z.string().datetime({ offset: true }),
  updatedBy: z.string().nullable(),
  xmin: z.number(),
});

// Backend: UpsertAppBudgetRequest (AdminBudgetEndpoints.cs:81).
// xmin omitted on first creation, required for in-place update (409 ConflictException on stale).
export const upsertAppBudgetRequestSchema = z.object({
  monthlyLimitAmount: z.number().positive(),
  monthlyLimitCurrency: z.literal('USD'),
  alertThresholdPct: z.number().int().min(1).max(99),
  criticalThresholdPct: z.number().int().min(2).max(100),
  xmin: z.number().optional().nullable(),
});

// Backend: AppBudgetUpsertResult — wire shape returned by UpsertAppBudgetCommand handler.
// Mirrors `AlertChannelUpsertResult` convention from #1840.
export const upsertAppBudgetResultSchema = z.object({
  id: z.string().uuid(),
  updatedAt: z.string().datetime({ offset: true }),
  xmin: z.number(),
});

export type SpendBreakdown = z.infer<typeof spendBreakdownSchema>;
export type AppBudget = z.infer<typeof appBudgetSchema>;
export type UpsertAppBudgetRequest = z.infer<typeof upsertAppBudgetRequestSchema>;
export type UpsertAppBudgetResult = z.infer<typeof upsertAppBudgetResultSchema>;
