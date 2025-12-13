/**
 * Testing Dashboard Schemas (Issue #2139)
 *
 * Zod validation schemas for testing metrics
 */

import { z } from 'zod';

// ========== Accessibility Metrics ==========

export const AccessibilityIssueSchema = z.object({
  page: z.string(),
  issue: z.string(),
  severity: z.enum(['low', 'medium', 'high']),
});

export const WcagComplianceSchema = z.object({
  levelA: z.number(),
  levelAA: z.number(),
  levelAAA: z.number(),
});

export const AccessibilityMetricsSchema = z.object({
  lighthouseScore: z.number(),
  axeViolations: z.number(),
  wcagCompliance: WcagComplianceSchema,
  testedPages: z.number(),
  criticalIssues: z.array(AccessibilityIssueSchema),
  lastRun: z.string(),
});

export type AccessibilityIssue = z.infer<typeof AccessibilityIssueSchema>;
export type WcagCompliance = z.infer<typeof WcagComplianceSchema>;
export type AccessibilityMetrics = z.infer<typeof AccessibilityMetricsSchema>;

// ========== Performance Metrics ==========

export const CoreWebVitalsSchema = z.object({
  lcp: z.number(), // Largest Contentful Paint (seconds)
  fid: z.number(), // First Input Delay (milliseconds)
  cls: z.number(), // Cumulative Layout Shift
});

export const BudgetItemSchema = z.object({
  current: z.number(),
  budget: z.number(),
  unit: z.string(),
});

export const BudgetStatusSchema = z.object({
  js: BudgetItemSchema,
  css: BudgetItemSchema,
  images: BudgetItemSchema,
});

export const SlowPageSchema = z.object({
  page: z.string(),
  loadTime: z.number(),
});

export const PerformanceMetricsSchema = z.object({
  lighthouseScore: z.number(),
  coreWebVitals: CoreWebVitalsSchema,
  budgetStatus: BudgetStatusSchema,
  slowestPages: z.array(SlowPageSchema),
  lastRun: z.string(),
});

export type CoreWebVitals = z.infer<typeof CoreWebVitalsSchema>;
export type BudgetItem = z.infer<typeof BudgetItemSchema>;
export type BudgetStatus = z.infer<typeof BudgetStatusSchema>;
export type SlowPage = z.infer<typeof SlowPageSchema>;
export type PerformanceMetrics = z.infer<typeof PerformanceMetricsSchema>;

// ========== E2E Metrics ==========

export const CriticalJourneySchema = z.object({
  name: z.string(),
  status: z.enum(['pass', 'fail', 'flaky']),
  duration: z.number(),
});

export const E2EMetricsSchema = z.object({
  coverage: z.number(), // percentage
  passRate: z.number(), // percentage
  flakyRate: z.number(), // percentage
  totalTests: z.number(),
  executionTime: z.number(), // minutes
  criticalJourneys: z.array(CriticalJourneySchema),
  lastRun: z.string(),
});

export type CriticalJourney = z.infer<typeof CriticalJourneySchema>;
export type E2EMetrics = z.infer<typeof E2EMetricsSchema>;
