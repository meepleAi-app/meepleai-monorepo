/**
 * ISSUE-3778: Multi-Agent Dashboard Schema
 * Types and mock data for unified agent monitoring
 */

import { z } from 'zod';

export const AgentStatsSchema = z.object({
  agentType: z.enum(['tutor', 'arbitro', 'decisore']),
  totalQueries: z.number(),
  successRate: z.number(), // 0-100
  avgResponseTime: z.number(), // milliseconds
  p95ResponseTime: z.number(),
  totalCost: z.number(), // USD
  status: z.enum(['healthy', 'degraded', 'offline']),
});

export type AgentStats = z.infer<typeof AgentStatsSchema>;

export const MultiAgentDashboardDataSchema = z.object({
  totalQueries: z.number(),
  totalCost: z.number(),
  avgResponseTime: z.number(),
  agents: z.array(AgentStatsSchema),
  costTrend: z.array(
    z.object({
      date: z.string(),
      tutor: z.number(),
      arbitro: z.number(),
      decisore: z.number(),
    })
  ),
});

export type MultiAgentDashboardData = z.infer<typeof MultiAgentDashboardDataSchema>;

// Mock data (deterministic for SSR)
export const mockMultiAgentDashboardData: MultiAgentDashboardData = {
  totalQueries: 1547,
  totalCost: 42.35,
  avgResponseTime: 385,
  agents: [
    {
      agentType: 'tutor',
      totalQueries: 892,
      successRate: 96.5,
      avgResponseTime: 245,
      p95ResponseTime: 580,
      totalCost: 18.2,
      status: 'healthy',
    },
    {
      agentType: 'arbitro',
      totalQueries: 445,
      successRate: 99.1,
      avgResponseTime: 87,
      p95ResponseTime: 95,
      totalCost: 8.9,
      status: 'healthy',
    },
    {
      agentType: 'decisore',
      totalQueries: 210,
      successRate: 94.3,
      avgResponseTime: 1823,
      p95ResponseTime: 3200,
      totalCost: 15.25,
      status: 'healthy',
    },
  ],
  costTrend: [
    { date: '2026-02-05', tutor: 12.5, arbitro: 6.2, decisore: 10.1 },
    { date: '2026-02-06', tutor: 13.8, arbitro: 6.8, decisore: 11.3 },
    { date: '2026-02-07', tutor: 15.2, arbitro: 7.5, decisore: 12.8 },
    { date: '2026-02-08', tutor: 16.1, arbitro: 7.9, decisore: 13.5 },
    { date: '2026-02-09', tutor: 17.3, arbitro: 8.4, decisore: 14.2 },
    { date: '2026-02-10', tutor: 17.8, arbitro: 8.7, decisore: 14.8 },
    { date: '2026-02-11', tutor: 18.2, arbitro: 8.9, decisore: 15.25 },
  ],
};
