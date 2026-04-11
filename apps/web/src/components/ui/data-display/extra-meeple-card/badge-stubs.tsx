/**
 * Stub badge components replacing deleted meeple-card-features badge modules.
 * Simplified replacements with extended prop surface to satisfy all consumers.
 */
import React from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// AgentStatusBadge
// ============================================================================

export interface AgentStatusBadgeProps {
  status: string;
  className?: string;
}

export function AgentStatusBadge({ status, className }: AgentStatusBadgeProps) {
  const variantClass =
    status === 'active' || status === 'Active'
      ? 'bg-emerald-100 text-emerald-700'
      : status === 'idle' || status === 'Idle'
        ? 'bg-slate-100 text-slate-600'
        : 'bg-amber-100 text-amber-700';

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium',
        variantClass,
        className
      )}
    >
      {status}
    </span>
  );
}

// ============================================================================
// AgentModelInfo
// ============================================================================

export interface AgentModelInfoProps {
  /** Model display name */
  modelName?: string;
  model?: string;
  strategyName?: string;
  className?: string;
}

export function AgentModelInfo({ modelName, model, strategyName, className }: AgentModelInfoProps) {
  const display = modelName ?? model;
  return (
    <div className={cn('text-xs text-muted-foreground', className)}>
      {display && <span className="font-mono">{display}</span>}
      {strategyName && <span className="ml-2 opacity-70">{strategyName}</span>}
    </div>
  );
}

// ============================================================================
// AgentStatsDisplay
// ============================================================================

export interface AgentStatsDisplayProps {
  /** Stats object */
  stats?: {
    invocationCount?: number;
    lastExecutedAt?: string | null;
    avgResponseTimeMs?: number;
  };
  invocationCount?: number;
  lastInvokedAt?: string | null;
  className?: string;
}

export function AgentStatsDisplay({
  stats,
  invocationCount,
  lastInvokedAt,
  className,
}: AgentStatsDisplayProps) {
  const count = stats?.invocationCount ?? invocationCount;
  const lastAt = stats?.lastExecutedAt ?? lastInvokedAt;
  return (
    <div className={cn('flex gap-4 text-xs text-muted-foreground', className)}>
      {count !== undefined && <span>{count} invocations</span>}
      {lastAt && <span>Last: {new Date(lastAt).toLocaleDateString()}</span>}
    </div>
  );
}

// ============================================================================
// KbStatusBadge / DocumentStatusBadge
// ============================================================================

export interface KbStatusBadgeProps {
  status: string;
  className?: string;
}

export function KbStatusBadge({ status, className }: KbStatusBadgeProps) {
  const variantClass =
    status === 'indexed' || status === 'Completed'
      ? 'bg-emerald-100 text-emerald-700'
      : status === 'processing' || status === 'Embedding' || status === 'Indexing'
        ? 'bg-blue-100 text-blue-700'
        : status === 'failed' || status === 'Failed'
          ? 'bg-red-100 text-red-700'
          : 'bg-slate-100 text-slate-600';

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium',
        variantClass,
        className
      )}
    >
      {status}
    </span>
  );
}

// Alias
export const DocumentStatusBadge = KbStatusBadge;

// ============================================================================
// ChatStatusBadge
// ============================================================================

export interface ChatStatusBadgeProps {
  status: string;
  className?: string;
}

export function ChatStatusBadge({ status, className }: ChatStatusBadgeProps) {
  const variantClass =
    status === 'active'
      ? 'bg-emerald-100 text-emerald-700'
      : status === 'archived'
        ? 'bg-slate-100 text-slate-600'
        : 'bg-amber-100 text-amber-700';

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium',
        variantClass,
        className
      )}
    >
      {status}
    </span>
  );
}
