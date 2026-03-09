/**
 * DebugStepCard - Collapsible card for a single RAG pipeline debug step
 * Issue #4916: Tab Debug sulla chat — input/output per ogni step RAG
 */

'use client';

import { useState } from 'react';

import { ChevronDown, ChevronRight, Clock } from 'lucide-react';

import type { DebugStep } from '@/hooks/useAgentChatStream';
import { cn } from '@/lib/utils';

// ─── Step icon/color config ───────────────────────────────────────────────────

const STEP_COLORS: Record<number, string> = {
  10: 'text-purple-600 dark:text-purple-400 bg-purple-50 dark:bg-purple-500/10 border-purple-200 dark:border-purple-500/20',
  11: 'text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-500/10 border-blue-200 dark:border-blue-500/20',
  12: 'text-cyan-600 dark:text-cyan-400 bg-cyan-50 dark:bg-cyan-500/10 border-cyan-200 dark:border-cyan-500/20',
  13: 'text-teal-600 dark:text-teal-400 bg-teal-50 dark:bg-teal-500/10 border-teal-200 dark:border-teal-500/20',
  14: 'text-green-600 dark:text-green-400 bg-green-50 dark:bg-green-500/10 border-green-200 dark:border-green-500/20',
  15: 'text-yellow-600 dark:text-yellow-400 bg-yellow-50 dark:bg-yellow-500/10 border-yellow-200 dark:border-yellow-500/20',
  16: 'text-orange-600 dark:text-orange-400 bg-orange-50 dark:bg-orange-500/10 border-orange-200 dark:border-orange-500/20',
  17: 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-500/10 border-red-200 dark:border-red-500/20',
  18: 'text-indigo-600 dark:text-indigo-400 bg-indigo-50 dark:bg-indigo-500/10 border-indigo-200 dark:border-indigo-500/20',
  19: 'text-slate-600 dark:text-slate-400 bg-slate-50 dark:bg-slate-500/10 border-slate-200 dark:border-slate-500/20',
  20: 'text-zinc-600 dark:text-zinc-400 bg-zinc-50 dark:bg-zinc-500/10 border-zinc-200 dark:border-zinc-500/20',
  22: 'text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-500/10 border-emerald-200 dark:border-emerald-500/20',
};

const DEFAULT_COLOR = 'text-muted-foreground bg-muted/30 border-border/50';

// ─── JSON highlighter ─────────────────────────────────────────────────────────

function JsonBlock({ data, redact }: { data: unknown; redact?: string[] }) {
  let displayData = data;
  if (redact && redact.length > 0 && data && typeof data === 'object') {
    const copy = { ...(data as Record<string, unknown>) };
    for (const key of redact) {
      if (key in copy) {
        copy[key] = '[redacted]';
      }
    }
    displayData = copy;
  }

  return (
    <pre className="text-[11px] font-mono whitespace-pre-wrap break-all bg-muted/50 dark:bg-muted/20 rounded p-2 overflow-auto max-h-48 text-foreground/80">
      {JSON.stringify(displayData, null, 2)}
    </pre>
  );
}

// ─── Typology Profile View ────────────────────────────────────────────────────

function TypologyProfileView({ payload }: { payload: unknown }) {
  const p = payload as Record<string, unknown>;
  return (
    <div className="text-xs space-y-1 mt-1" data-testid="typology-profile-view">
      <div className="font-semibold">{String(p.typology ?? 'Unknown')} Profile</div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-0.5 font-mono text-[11px]">
        <span>TopK: {String(p.topK)}</span>
        <span>MinScore: {String(p.minScore)}</span>
        <span>Strategy: {String(p.searchStrategy)}</span>
        <span>Temp: {String(p.temperature)}</span>
        <span>MaxTokens: {String(p.maxTokens)}</span>
      </div>
    </div>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export interface DebugStepCardProps {
  step: DebugStep;
  index: number;
  /** If false, system_prompt will be hidden (non-admin users) */
  showSystemPrompt?: boolean;
}

export function DebugStepCard({ step, index, showSystemPrompt = false }: DebugStepCardProps) {
  const [isOpen, setIsOpen] = useState(false);
  const colorClass = STEP_COLORS[step.type] ?? DEFAULT_COLOR;

  const redactKeys = !showSystemPrompt && step.type === 16 ? ['systemPrompt'] : [];

  return (
    <div className={cn('rounded-lg border text-xs', colorClass)}>
      {/* Header */}
      <button
        type="button"
        onClick={() => setIsOpen(v => !v)}
        className="w-full flex items-center gap-2 px-3 py-2 text-left"
        aria-expanded={isOpen}
      >
        {isOpen ? (
          <ChevronDown className="h-3 w-3 shrink-0" />
        ) : (
          <ChevronRight className="h-3 w-3 shrink-0" />
        )}
        <span className="font-mono font-semibold">
          {index + 1}. {step.name}
        </span>
        {step.latencyMs !== undefined && (
          <span className="ml-auto flex items-center gap-1 opacity-70">
            <Clock className="h-2.5 w-2.5" />
            {step.latencyMs}ms
          </span>
        )}
      </button>

      {/* Body */}
      {isOpen && (
        <div className="px-3 pb-3 space-y-2 border-t border-inherit/30">
          <p className="text-[10px] text-muted-foreground mt-1">
            {new Date(step.timestamp).toLocaleTimeString('it-IT', {
              hour: '2-digit',
              minute: '2-digit',
              second: '2-digit',
            })}
          </p>
          {step.type === 22 ? (
            <TypologyProfileView payload={step.payload} />
          ) : (
            <JsonBlock data={step.payload} redact={redactKeys} />
          )}
        </div>
      )}
    </div>
  );
}
