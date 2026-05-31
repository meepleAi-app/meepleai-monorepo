'use client';

export interface LatencyBreakdown {
  retrievalMs: number;
  rerankMs: number;
  llmMs: number;
  postMs: number;
}

interface LatencyBreakdownBarProps {
  breakdown: LatencyBreakdown | null;
  totalMs: number;
}

const STAGES: ReadonlyArray<{
  key: keyof LatencyBreakdown;
  id: 'retrieval' | 'rerank' | 'llm' | 'post';
  label: string;
  toneClass: string;
}> = [
  { key: 'retrievalMs', id: 'retrieval', label: 'Retrieval', toneClass: 'bg-entity-toolkit' },
  { key: 'rerankMs', id: 'rerank', label: 'Rerank', toneClass: 'bg-entity-session' },
  { key: 'llmMs', id: 'llm', label: 'LLM', toneClass: 'bg-entity-agent' },
  { key: 'postMs', id: 'post', label: 'Post', toneClass: 'bg-entity-event' },
];

/**
 * Segmented horizontal bar for a single AI query's latency split.
 *
 * The drill endpoint (#1722 sub-task BE) is expected to return the
 * per-stage durations. When it returns null/undefined (today's
 * common case until the BE lands), we show an "unavailable" caption
 * with the total only, so the panel keeps the slot visually
 * reserved without lying about data.
 */
export function LatencyBreakdownBar({ breakdown, totalMs }: LatencyBreakdownBarProps) {
  if (!breakdown) {
    return (
      <section className="space-y-1.5">
        <header className="flex items-center justify-between text-[11px] text-muted-foreground">
          <span className="font-mono uppercase tracking-wider">Latency breakdown</span>
          <span className="font-mono tabular-nums">{totalMs} ms total</span>
        </header>
        <p className="rounded-md border border-dashed border-border/60 bg-muted/30 px-2 py-1.5 text-[11px] text-muted-foreground">
          breakdown unavailable — drill endpoint pending
        </p>
      </section>
    );
  }

  const safeTotal = Math.max(totalMs, 1);

  return (
    <section className="space-y-1.5">
      <header className="flex items-center justify-between text-[11px] text-muted-foreground">
        <span className="font-mono uppercase tracking-wider">Latency breakdown</span>
        <span className="font-mono tabular-nums">{totalMs} ms total</span>
      </header>
      <div className="flex h-3 overflow-hidden rounded-full border border-border/60">
        {STAGES.map(stage => {
          const value = breakdown[stage.key];
          const widthPct = (value / safeTotal) * 100;
          if (widthPct <= 0) return null;
          return (
            <div
              key={stage.id}
              data-stage={stage.id}
              className={`${stage.toneClass} h-full`}
              style={{ width: `${widthPct}%` }}
              title={`${stage.label} · ${value} ms`}
            />
          );
        })}
      </div>
      <ul role="list" aria-label="Latency breakdown" className="grid grid-cols-2 gap-x-3 gap-y-1">
        {STAGES.map(stage => (
          <li
            key={stage.id}
            className="flex items-center gap-1.5 text-[11px] text-muted-foreground"
          >
            <span className={`inline-block h-2 w-2 rounded-sm ${stage.toneClass}`} aria-hidden />
            <span>{stage.label}</span>
            <span className="ml-auto font-mono tabular-nums text-foreground">
              {breakdown[stage.key]} ms
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
