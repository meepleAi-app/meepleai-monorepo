'use client';

/**
 * DebugEventCard - Single debug event card in the timeline
 *
 * Renders a compact card with icon, label, elapsed time, and expandable detail.
 * Type-specific rendering for search results, prompts, costs, etc.
 */

import { useState } from 'react';
import {
  ChevronDownIcon,
  ChevronRightIcon,
  RouteIcon,
  SettingsIcon,
  SearchIcon,
  ListIcon,
  PlugIcon,
  ShieldCheckIcon,
  FileTextIcon,
  DollarSignIcon,
  DatabaseIcon,
  HardDriveIcon,
  CheckCircle2Icon,
  XCircleIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type { DebugEvent } from '@/hooks/useDebugChatStream';

// Event type to icon/color mapping
const EVENT_CONFIG: Record<number, { icon: React.ElementType; color: string; bg: string }> = {
  10: { icon: RouteIcon, color: 'text-purple-400', bg: 'bg-purple-500/10' },     // AgentRouter
  11: { icon: SettingsIcon, color: 'text-blue-400', bg: 'bg-blue-500/10' },      // StrategySelected
  12: { icon: SearchIcon, color: 'text-amber-400', bg: 'bg-amber-500/10' },      // RetrievalStart
  13: { icon: ListIcon, color: 'text-green-400', bg: 'bg-green-500/10' },        // RetrievalResults
  14: { icon: PlugIcon, color: 'text-cyan-400', bg: 'bg-cyan-500/10' },          // PluginExecution
  15: { icon: ShieldCheckIcon, color: 'text-emerald-400', bg: 'bg-emerald-500/10' }, // ValidationLayer
  16: { icon: FileTextIcon, color: 'text-orange-400', bg: 'bg-orange-500/10' },  // PromptContext
  17: { icon: DollarSignIcon, color: 'text-yellow-400', bg: 'bg-yellow-500/10' }, // CostUpdate
  18: { icon: DatabaseIcon, color: 'text-indigo-400', bg: 'bg-indigo-500/10' },  // SearchDetails
  19: { icon: HardDriveIcon, color: 'text-sky-400', bg: 'bg-sky-500/10' },       // CacheCheck
  20: { icon: CheckCircle2Icon, color: 'text-teal-400', bg: 'bg-teal-500/10' },  // DocumentCheck
};

function formatMs(ms: number): string {
  if (ms < 1) return '<1ms';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

interface DebugEventCardProps {
  event: DebugEvent;
}

export function DebugEventCard({ event }: DebugEventCardProps) {
  const [expanded, setExpanded] = useState(false);
  const config = EVENT_CONFIG[event.type] || {
    icon: DatabaseIcon,
    color: 'text-gray-400',
    bg: 'bg-gray-500/10',
  };
  const Icon = config.icon;
  const data = event.data as Record<string, unknown> | null;

  // Determine success/failure indicator
  const hasStatus = data && typeof data === 'object';
  const isSuccess = hasStatus && (
    data.hit === true ||
    data.allReady === true ||
    data.isValid === true ||
    (typeof data.count === 'number' && (data.count as number) > 0)
  );
  const isFailure = hasStatus && (
    data.hit === false ||
    data.allReady === false ||
    data.isValid === false
  );

  return (
    <div className={cn('rounded-md border border-border/50', config.bg)}>
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-muted/30 transition-colors"
        type="button"
      >
        <Icon className={cn('h-4 w-4 shrink-0', config.color)} />
        <span className="font-medium text-foreground truncate flex-1">
          {event.typeName}
        </span>

        {isSuccess && <CheckCircle2Icon className="h-3.5 w-3.5 text-green-500 shrink-0" />}
        {isFailure && <XCircleIcon className="h-3.5 w-3.5 text-red-500 shrink-0" />}

        <span className="text-xs text-muted-foreground tabular-nums shrink-0">
          +{formatMs(event.elapsedMs)}
        </span>

        {expanded
          ? <ChevronDownIcon className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
          : <ChevronRightIcon className="h-3.5 w-3.5 text-muted-foreground shrink-0" />}
      </button>

      {expanded && data && (
        <div className="px-3 pb-3 pt-1 border-t border-border/30">
          <EventDetail type={event.type} data={data} />
        </div>
      )}
    </div>
  );
}

function EventDetail({ type, data }: { type: number; data: Record<string, unknown> }) {
  switch (type) {
    case 13: // RetrievalResults
      return <RetrievalResultsDetail data={data} />;
    case 16: // PromptContext
      return <PromptContextDetail data={data} />;
    case 17: // CostUpdate
      return <CostUpdateDetail data={data} />;
    case 15: // ValidationLayer
      return <ValidationDetail data={data} />;
    default:
      return <JsonDetail data={data} />;
  }
}

function RetrievalResultsDetail({ data }: { data: Record<string, unknown> }) {
  const items = (data.items as Array<Record<string, unknown>>) || [];
  return (
    <div className="space-y-1.5">
      <div className="text-xs text-muted-foreground">
        {data.count as number} results in {formatMs(data.durationMs as number)}
      </div>
      {items.length > 0 && (
        <div className="text-xs space-y-1">
          {items.map((item, i) => (
            <div key={i} className="flex justify-between text-muted-foreground">
              <span className="truncate mr-2 font-mono">{item.documentId as string}</span>
              <span className="tabular-nums text-foreground">
                {((item.score as number) * 100).toFixed(1)}%
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function PromptContextDetail({ data }: { data: Record<string, unknown> }) {
  return (
    <div className="space-y-2">
      <div className="text-xs text-muted-foreground">
        ~{data.estimatedTokens as number} tokens estimated
      </div>
      <details className="text-xs">
        <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
          System prompt
        </summary>
        <pre className="mt-1 max-h-32 overflow-auto rounded bg-muted/50 p-2 text-[10px] leading-tight whitespace-pre-wrap">
          {data.systemPrompt as string}
        </pre>
      </details>
      <details className="text-xs">
        <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
          User prompt
        </summary>
        <pre className="mt-1 max-h-32 overflow-auto rounded bg-muted/50 p-2 text-[10px] leading-tight whitespace-pre-wrap">
          {data.userPrompt as string}
        </pre>
      </details>
    </div>
  );
}

function CostUpdateDetail({ data }: { data: Record<string, unknown> }) {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
      <span className="text-muted-foreground">Prompt tokens</span>
      <span className="tabular-nums text-right">{data.promptTokens as number}</span>
      <span className="text-muted-foreground">Completion tokens</span>
      <span className="tabular-nums text-right">{data.completionTokens as number}</span>
      <span className="text-muted-foreground">Total tokens</span>
      <span className="tabular-nums text-right font-medium">{data.totalTokens as number}</span>
      {data.costUsd != null && (
        <>
          <span className="text-muted-foreground">Cost</span>
          <span className="tabular-nums text-right">${(data.costUsd as number).toFixed(6)}</span>
        </>
      )}
      {data.modelId != null && (
        <>
          <span className="text-muted-foreground">Model</span>
          <span className="text-right truncate">{String(data.modelId)}</span>
        </>
      )}
    </div>
  );
}

function ValidationDetail({ data }: { data: Record<string, unknown> }) {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
      <span className="text-muted-foreground">Layer</span>
      <span>#{data.layerNumber as number} {data.layerName as string}</span>
      <span className="text-muted-foreground">Valid</span>
      <span className={data.isValid ? 'text-green-500' : 'text-red-500'}>
        {data.isValid ? 'Yes' : 'No'}
      </span>
      <span className="text-muted-foreground">Duration</span>
      <span className="tabular-nums">{formatMs(data.durationMs as number)}</span>
      {data.details != null && (
        <>
          <span className="text-muted-foreground">Details</span>
          <span className="truncate">{String(data.details)}</span>
        </>
      )}
    </div>
  );
}

function JsonDetail({ data }: { data: Record<string, unknown> }) {
  return (
    <pre className="max-h-40 overflow-auto rounded bg-muted/50 p-2 text-[10px] leading-tight whitespace-pre-wrap">
      {JSON.stringify(data, null, 2)}
    </pre>
  );
}
