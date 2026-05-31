'use client';

import { X } from 'lucide-react';

import type { AiRequest } from '@/lib/api/schemas';

interface QueryDrillPanelProps {
  query: AiRequest | null;
  onClose: () => void;
}

/**
 * Right-side drill-down panel for a single AI request.
 *
 * Shows the prompt, response snippet, and metadata grid. Chunks +
 * latency breakdown require the dedicated drill endpoint (#1722
 * sub-task BE — `GET /api/v1/admin/ai/queries/{id}/drill`); until
 * then we render a "limited drill" badge and surface only what the
 * `/api/v1/admin/requests` payload already exposes.
 */
export function QueryDrillPanel({ query, onClose }: QueryDrillPanelProps) {
  if (!query) return null;

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
            <span className="ml-auto inline-flex items-center rounded-full bg-warning/15 px-2 py-0.5 text-[10px] font-medium text-warning">
              limited drill
            </span>
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
