import { z } from 'zod';

// Issue #1838 SP5 F4-C5 — Cost breakdown by provider and by feature.
// Backend: CostBreakdownByProviderDto + CostBreakdownByFeatureDto.

export const costBreakdownRangeSchema = z.enum(['7d', '30d', '90d', '1y']);

const providerEntrySchema = z.object({
  provider: z.string(),
  cost: z.number(),
});

const providerTotalSchema = z.object({
  provider: z.string(),
  totalCost: z.number(),
});

const breakdownDaySchema = z.object({
  date: z.string().datetime({ offset: true }),
  providers: z.array(providerEntrySchema),
  total: z.number(),
});

// CostBreakdownByProviderDto from GetCostBreakdownByProviderQuery.cs.
// Days are ASC by date; providerTotals are DESC by total so the FE can pick
// the stack order without re-sorting.
export const costBreakdownByProviderSchema = z.object({
  range: costBreakdownRangeSchema,
  fromDate: z.string().datetime({ offset: true }),
  toDate: z.string().datetime({ offset: true }),
  days: z.array(breakdownDaySchema),
  providerTotals: z.array(providerTotalSchema),
  grandTotal: z.number(),
});

const featureSchema = z.object({
  feature: z.string(),
  totalCost: z.number(),
  percentageOfTotal: z.number(),
  providers: z.array(providerEntrySchema),
});

// CostBreakdownByFeatureDto from GetCostBreakdownByFeatureQuery.cs.
// Each feature carries an inline provider drill so the FE renders the
// expand-row without a secondary fetch.
export const costBreakdownByFeatureSchema = z.object({
  range: costBreakdownRangeSchema,
  fromDate: z.string().datetime({ offset: true }),
  toDate: z.string().datetime({ offset: true }),
  features: z.array(featureSchema),
  grandTotal: z.number(),
});

export type CostBreakdownRange = z.infer<typeof costBreakdownRangeSchema>;
export type ProviderEntry = z.infer<typeof providerEntrySchema>;
export type ProviderTotal = z.infer<typeof providerTotalSchema>;
export type BreakdownDay = z.infer<typeof breakdownDaySchema>;
export type CostBreakdownByProvider = z.infer<typeof costBreakdownByProviderSchema>;
export type CostBreakdownFeature = z.infer<typeof featureSchema>;
export type CostBreakdownByFeature = z.infer<typeof costBreakdownByFeatureSchema>;
