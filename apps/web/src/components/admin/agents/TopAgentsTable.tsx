'use client';

/**
 * TopAgentsTable Component
 * Issue #3382: Agent Metrics Dashboard
 *
 * Displays top agents in a table format with key metrics.
 */

import type { TopAgent } from '@/app/(authenticated)/admin/agents/metrics/client';
import { Badge } from '@/components/ui/data-display/badge';
import { cn } from '@/lib/utils';


// ============================================================================
// Types
// ============================================================================

interface TopAgentsTableProps {
  agents: TopAgent[];
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatCost(cost: number): string {
  if (cost >= 1) return `$${cost.toFixed(2)}`;
  if (cost >= 0.01) return `$${cost.toFixed(3)}`;
  return `$${cost.toFixed(4)}`;
}

function formatLatency(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms)}ms`;
}

function getConfidenceColor(confidence: number): string {
  if (confidence >= 0.9) return 'bg-emerald-500';
  if (confidence >= 0.7) return 'bg-blue-500';
  if (confidence >= 0.5) return 'bg-amber-500';
  return 'bg-red-500';
}

// ============================================================================
// Component
// ============================================================================

export function TopAgentsTable({ agents, className }: TopAgentsTableProps) {
  if (agents.length === 0) {
    return (
      <div className="flex h-32 items-center justify-center text-muted-foreground">
        No agents found
      </div>
    );
  }

  return (
    <div className={cn('overflow-x-auto', className)}>
      <table className="w-full">
        <thead>
          <tr className="border-b text-left text-xs text-muted-foreground">
            <th className="pb-2 font-medium">Agent</th>
            <th className="pb-2 text-right font-medium">Invocations</th>
            <th className="pb-2 text-right font-medium">Cost</th>
            <th className="pb-2 text-right font-medium">Confidence</th>
            <th className="pb-2 text-right font-medium">Latency</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {agents.map((agent, idx) => (
            <tr
              key={agent.typologyId}
              className="hover:bg-muted/50 transition-colors"
            >
              <td className="py-3">
                <div className="flex items-center gap-2">
                  <span className="flex h-6 w-6 items-center justify-center rounded-full bg-muted text-xs font-medium">
                    {idx + 1}
                  </span>
                  <span className="font-medium truncate max-w-[200px]" title={agent.typologyName}>
                    {agent.typologyName}
                  </span>
                </div>
              </td>
              <td className="py-3 text-right font-mono text-sm">
                {agent.invocations.toLocaleString()}
              </td>
              <td className="py-3 text-right font-mono text-sm">
                {formatCost(agent.cost)}
              </td>
              <td className="py-3 text-right">
                <Badge
                  variant="secondary"
                  className={cn(
                    'text-white text-xs',
                    getConfidenceColor(agent.avgConfidence)
                  )}
                >
                  {(agent.avgConfidence * 100).toFixed(0)}%
                </Badge>
              </td>
              <td className="py-3 text-right font-mono text-sm text-muted-foreground">
                {formatLatency(agent.avgLatencyMs)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
