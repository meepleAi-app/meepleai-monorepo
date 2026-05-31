'use client';

import { X, FileText } from 'lucide-react';

import type { AiRequest, RetrievedChunkDto } from '@/lib/api/schemas';

import { LatencyBreakdownBar, type LatencyBreakdown } from './LatencyBreakdownBar';

interface QueryDrillPanelProps {
  query: AiRequest | null;
  onClose: () => void;
  /**
   * Per-stage latency split. Populated by #1728 drill endpoint when
   * the pipeline has been instrumented; null otherwise (FE renders
   * "breakdown unavailable" fallback).
   */
  breakdown?: LatencyBreakdown | null;
  /**
   * Retrieved chunks for this query. Populated by #1728 drill
   * endpoint when the pipeline captured them. Empty array surfaces
   * the "no chunks recorded" empty state without the "limited drill"
   * badge.
   */
  chunks?: readonly RetrievedChunkDto[] | null;
  /**
   * True when the drill endpoint has resolved successfully (even if
   * chunks is empty). Controls the "limited drill" badge — hides it
   * when the BE has spoken, surfaces it when we only have the base
   * `/admin/requests` payload.
   */
  drillReady?: boolean;
}

/**
 * Right-side drill-down panel for a single AI request.
 *
 * When the dedicated drill endpoint (`GET /api/v1/admin/ai/queries/{id}/drill`)
 * has resolved (`drillReady=true`), surfaces chunks + breakdown. Falls
 * back to a "limited drill" badge for legacy callers that only have
 * the `/admin/requests` payload.
 */
export function QueryDrillPanel({
  query,
  onClose,
  breakdown = null,
  chunks = null,
  drillReady = false,
}: QueryDrillPanelProps) {
  if (!query) return null;
  const showLimitedBadge = !drillReady;

  return (
    <aside
      role="region"
      aria-label="Query drill-down"
      className="sticky top-20 h-[calc(100vh-6rem)] overflow-y-auto rounded-xl border border-border/60 bg-card/70 backdrop-blur-md p-4 space-y-4"
    >
      <header className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-mono text-[11px] text-muted-foreground truncate">
              {query.endpoint}
            </span>
            <StatusPill status={query.status} />
            {showLimitedBadge && (
              <span className="ml-auto inline-flex items-center rounded-full bg-warning/15 px-2 py-0.5 text-[10px] font-medium text-warning">
                limited drill
              </span>
            )}
          </div>
          <div className="mt-1 flex items-center gap-3 text-[11px] text-muted-foreground tabular-nums">
            <span>{query.model ?? '—'}</span>
            <span>{query.latencyMs} ms</span>
            <span>{query.tokenCount.toLocaleString()} tokens</span>
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close drill"
          className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          <X className="h-4 w-4" />
        </button>
      </header>

      <Section label="Query">
        <p className="font-quicksand text-sm text-foreground whitespace-pre-wrap break-words">
          {query.query ?? '—'}
        </p>
      </Section>

      <Section label="Response">
        <p className="font-quicksand text-sm text-foreground/85 whitespace-pre-wrap break-words">
          {query.responseSnippet ?? '—'}
        </p>
      </Section>

      <LatencyBreakdownBar breakdown={breakdown} totalMs={query.latencyMs} />

      {drillReady && (
        <Section label={`Retrieved chunks (${chunks?.length ?? 0})`}>
          {chunks && chunks.length > 0 ? (
            <ul className="space-y-2">
              {chunks.map(c => (
                <li
                  key={c.id}
                  className="rounded-md border border-border/60 bg-background p-2 space-y-1"
                >
                  <div className="flex items-center gap-2 text-[10px] text-muted-foreground">
                    <FileText className="h-3 w-3" />
                    <span className="font-mono truncate">{c.pdfName}</span>
                    <span className="ml-auto font-mono tabular-nums">p.{c.page}</span>
                    <span className="font-mono tabular-nums">#{c.chunkIndex}</span>
                    <span className="font-mono tabular-nums">{c.score.toFixed(2)}</span>
                  </div>
                  <p className="text-[12px] text-foreground/85 line-clamp-3 break-words">
                    {c.text}
                  </p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="rounded-md border border-dashed border-border/60 bg-muted/30 px-2 py-1.5 text-[11px] text-muted-foreground">
              no chunks recorded for this query
            </p>
          )}
        </Section>
      )}

      <Section label="Metadata">
        <dl className="grid grid-cols-2 gap-2 text-[11px]">
          <Metric label="Prompt tokens" value={query.promptTokens.toLocaleString()} />
          <Metric label="Completion tokens" value={query.completionTokens.toLocaleString()} />
          <Metric
            label="Confidence"
            value={query.confidence != null ? `${(query.confidence * 100).toFixed(0)}%` : '—'}
          />
          <Metric label="Finish" value={query.finishReason ?? '—'} />
        </dl>
      </Section>

      {query.errorMessage && (
        <Section label="Error">
          <p className="rounded-md border border-destructive/40 bg-destructive/10 p-2 font-mono text-[11px] text-destructive break-words">
            {query.errorMessage}
          </p>
        </Section>
      )}
    </aside>
  );
}

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="mb-1.5 font-mono text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
        {label}
      </h3>
      {children}
    </section>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-mono text-foreground tabular-nums">{value}</dd>
    </>
  );
}

function StatusPill({ status }: { status: string }) {
  const tone =
    status === 'Success' ? 'bg-success/15 text-success' : 'bg-destructive/15 text-destructive';
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${tone}`}
    >
      {status}
    </span>
  );
}
