import { z } from 'zod';

/**
 * Agent Test Result schemas for Issue #3379: Test Results History & Persistence.
 */

// Single test result entity
export const testResultSchema = z.object({
  id: z.string().uuid(),
  agentDefinitionId: z.string().uuid(),
  strategyOverride: z.string().optional().nullable(),
  modelUsed: z.string(),
  query: z.string(),
  response: z.string(),
  confidenceScore: z.number().min(0).max(1),
  tokensUsed: z.number().int().min(0),
  costEstimate: z.number().min(0),
  latencyMs: z.number().int().min(0),
  citationsJson: z.string().optional().nullable(),
  executedAt: z.string().datetime(),
  executedBy: z.string().uuid(),
  notes: z.string().optional().nullable(),
  isSaved: z.boolean(),
});

// Paginated list response
export const testResultListSchema = z.object({
  items: z.array(testResultSchema),
  totalCount: z.number().int().min(0),
  skip: z.number().int().min(0),
  take: z.number().int().min(1),
});

// Request to save a test result
export const saveTestResultRequestSchema = z.object({
  agentDefinitionId: z.string().uuid(),
  query: z.string().min(1),
  response: z.string().min(1),
  modelUsed: z.string().min(1),
  confidenceScore: z.number().min(0).max(1),
  tokensUsed: z.number().int().min(0),
  costEstimate: z.number().min(0),
  latencyMs: z.number().int().min(0),
  strategyOverride: z.string().optional().nullable(),
  citationsJson: z.string().optional().nullable(),
});

// Query parameters for filtering test results
export const testResultsQuerySchema = z.object({
  agentDefinitionId: z.string().uuid().optional(),
  from: z.string().datetime().optional(),
  to: z.string().datetime().optional(),
  savedOnly: z.boolean().optional(),
  skip: z.number().int().min(0).optional().default(0),
  take: z.number().int().min(1).max(100).optional().default(50),
});

// Type exports
export type TestResult = z.infer<typeof testResultSchema>;
export type TestResultList = z.infer<typeof testResultListSchema>;
export type SaveTestResultRequest = z.infer<typeof saveTestResultRequestSchema>;
export type TestResultsQuery = z.infer<typeof testResultsQuerySchema>;
