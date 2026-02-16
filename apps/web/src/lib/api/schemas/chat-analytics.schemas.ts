/**
 * Chat Analytics API Schemas (Issue #3714)
 *
 * Zod schemas for validating chat analytics responses.
 * Covers: thread counts, message stats, agent type breakdown, daily trends
 */

import { z } from 'zod';

// ========== Daily Chat Stats ==========

export const DailyChatStatsSchema = z.object({
  date: z.string(), // DateOnly serialized as "YYYY-MM-DD"
  totalCount: z.number().int().nonnegative(),
  activeCount: z.number().int().nonnegative(),
  closedCount: z.number().int().nonnegative(),
  messageCount: z.number().int().nonnegative(),
});

export type DailyChatStats = z.infer<typeof DailyChatStatsSchema>;

// ========== Chat Analytics DTO ==========

export const ChatAnalyticsDtoSchema = z.object({
  totalThreads: z.number().int().nonnegative(),
  activeThreads: z.number().int().nonnegative(),
  closedThreads: z.number().int().nonnegative(),
  totalMessages: z.number().int().nonnegative(),
  avgMessagesPerThread: z.number().nonnegative(),
  uniqueUsers: z.number().int().nonnegative(),
  threadsByAgentType: z.record(z.string(), z.number()),
  threadsByDay: z.array(DailyChatStatsSchema),
});

export type ChatAnalyticsDto = z.infer<typeof ChatAnalyticsDtoSchema>;
