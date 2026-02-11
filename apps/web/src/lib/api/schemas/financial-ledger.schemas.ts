/**
 * Financial Ledger Zod Schemas
 * Issue #3722: Manual Ledger CRUD
 * Issue #3723: Ledger Dashboard and Visualization
 */

import { z } from 'zod';

// ========== Enums ==========

export const LedgerEntryTypeSchema = z.enum(['Income', 'Expense']);
export type LedgerEntryType = z.infer<typeof LedgerEntryTypeSchema>;

// Map numeric values from backend to string labels
export const LEDGER_ENTRY_TYPE_MAP: Record<number, LedgerEntryType> = {
  0: 'Income',
  1: 'Expense',
};

export const LedgerCategorySchema = z.enum([
  'Subscription',
  'TokenPurchase',
  'TokenUsage',
  'PlatformFee',
  'Refund',
  'Operational',
  'Marketing',
  'Infrastructure',
  'Other',
]);
export type LedgerCategory = z.infer<typeof LedgerCategorySchema>;

export const LEDGER_CATEGORY_MAP: Record<number, LedgerCategory> = {
  0: 'Subscription',
  1: 'TokenPurchase',
  2: 'TokenUsage',
  3: 'PlatformFee',
  4: 'Refund',
  5: 'Operational',
  6: 'Marketing',
  7: 'Infrastructure',
  8: 'Other',
};

export const LedgerEntrySourceSchema = z.enum(['Auto', 'Manual']);
export type LedgerEntrySource = z.infer<typeof LedgerEntrySourceSchema>;

export const LEDGER_SOURCE_MAP: Record<number, LedgerEntrySource> = {
  0: 'Auto',
  1: 'Manual',
};

// ========== DTOs ==========

export const LedgerEntryDtoSchema = z.object({
  id: z.string().uuid(),
  date: z.string(),
  type: z.number(),
  category: z.number(),
  amount: z.number(),
  currency: z.string(),
  source: z.number(),
  description: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  createdByUserId: z.string().uuid().nullable().optional(),
  createdAt: z.string(),
  updatedAt: z.string().nullable().optional(),
});
export type LedgerEntryDto = z.infer<typeof LedgerEntryDtoSchema>;

export const LedgerEntriesResponseSchema = z.object({
  entries: z.array(LedgerEntryDtoSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});
export type LedgerEntriesResponse = z.infer<typeof LedgerEntriesResponseSchema>;

export const LedgerSummarySchema = z.object({
  totalIncome: z.number(),
  totalExpense: z.number(),
  netBalance: z.number(),
  from: z.string(),
  to: z.string(),
});
export type LedgerSummary = z.infer<typeof LedgerSummarySchema>;

export const CreateLedgerEntryResponseSchema = z.object({
  id: z.string().uuid(),
});
export type CreateLedgerEntryResponse = z.infer<typeof CreateLedgerEntryResponseSchema>;

// ========== Request Types ==========

export type CreateLedgerEntryRequest = {
  date: string;
  type: number;
  category: number;
  amount: number;
  currency: string;
  description?: string | null;
};

export type UpdateLedgerEntryRequest = {
  description?: string | null;
  category?: number | null;
  metadata?: string | null;
};

export type GetLedgerEntriesParams = {
  page?: number;
  pageSize?: number;
  type?: number;
  category?: number;
  source?: number;
  dateFrom?: string;
  dateTo?: string;
};

export type GetLedgerSummaryParams = {
  dateFrom: string;
  dateTo: string;
};

// ========== Dashboard Schemas (Issue #3723) ==========

export const MonthlyRevenueDataSchema = z.object({
  month: z.string(),
  revenue: z.number(),
  costs: z.number(),
});
export type MonthlyRevenueData = z.infer<typeof MonthlyRevenueDataSchema>;

export const CategoryBreakdownItemSchema = z.object({
  category: z.string(),
  amount: z.number(),
  percentage: z.number(),
  type: LedgerEntryTypeSchema,
});
export type CategoryBreakdownItem = z.infer<typeof CategoryBreakdownItemSchema>;

export const LedgerDashboardDataSchema = z.object({
  monthlyData: z.array(MonthlyRevenueDataSchema),
  categoryBreakdown: z.array(CategoryBreakdownItemSchema),
  currentMonthBalance: z.number(),
  currentMonthRevenue: z.number(),
  currentMonthCosts: z.number(),
  profitMargin: z.number(),
  profitMarginTrend: z.number(),
  recentEntries: z.array(LedgerEntryDtoSchema),
});
export type LedgerDashboardData = z.infer<typeof LedgerDashboardDataSchema>;
