/**
 * TopAgentsTable Component
 * Issue #3382: Agent Metrics Dashboard
 * Issue #4862: Migrated to EntityTableView design system
 *
 * Displays top agents using EntityTableView entity="agent" with amber accents,
 * sortable columns, and formatted metrics (cost, confidence, latency).
 */

'use client';

import React from 'react';

import type { TopAgent } from '@/app/(authenticated)/admin/agents/metrics/client';
import { Badge } from '@/components/ui/data-display/badge';
import { EntityTableView } from '@/components/ui/data-display/entity-list-view';
import type { TableColumnConfig } from '@/components/ui/data-display/entity-list-view/entity-list-view.types';
import { cn } from '@/lib/utils';

interface TopAgentsTableProps {
  agents: TopAgent[];
  className?: string;
}

function formatCost(cost: number): string {
  if (cost >= 1) return `$${cost.toFixed(2)}`;
  if (cost >= 0.01) return `$${cost.toFixed(3)}`;
  return `$${cost.toFixed(4)}`;
}

function formatLatency(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms)}ms`;
}

function getConfidenceColor(pct: string): string {
  const val = parseInt(pct, 10);
  if (val >= 90) return 'bg-emerald-500';
  if (val >= 70) return 'bg-blue-500';
  if (val >= 50) return 'bg-amber-500';
  return 'bg-red-500';
}

const tableColumns: TableColumnConfig[] = [
  {
    id: 'title',
    header: 'Agent',
    accessorKey: 'title',
  },
  {
    id: 'invocations',
    header: 'Invocations',
    accessorKey: 'meta_0',
    cell: (value) => (
      <span className="font-mono text-sm">{String(value ?? '—')}</span>
    ),
  },
  {
    id: 'cost',
    header: 'Cost',
    accessorKey: 'meta_1',
    cell: (value) => (
      <span className="font-mono text-sm">{String(value ?? '—')}</span>
    ),
  },
  {
    id: 'confidence',
    header: 'Confidence',
    accessorKey: 'meta_2',
    cell: (value) => {
      const pct = String(value ?? '0%');
      return (
        <Badge
          variant="secondary"
          className={cn('text-white text-xs', getConfidenceColor(pct))}
        >
          {pct}
        </Badge>
      );
    },
  },
  {
    id: 'latency',
    header: 'Latency',
    accessorKey: 'meta_3',
    cell: (value) => (
      <span className="font-mono text-sm text-muted-foreground">
        {String(value ?? '—')}
      </span>
    ),
  },
];

export function TopAgentsTable({ agents, className }: TopAgentsTableProps) {
  return (
    <div className={className}>
      <EntityTableView
        displayItems={agents}
        items={agents}
        entity="agent"
        renderItem={(agent) => ({
          title: agent.typologyName,
          id: agent.typologyId,
          metadata: [
            { value: agent.invocations.toLocaleString() },
            { value: formatCost(agent.cost) },
            { value: `${(agent.avgConfidence * 100).toFixed(0)}%` },
            { value: formatLatency(agent.avgLatencyMs) },
          ],
        })}
        tableColumns={tableColumns}
        emptyMessage="No agents found"
        data-testid="top-agents-table"
      />
    </div>
  );
}
