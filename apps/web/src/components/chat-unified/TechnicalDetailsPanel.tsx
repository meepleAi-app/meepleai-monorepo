'use client';

import { useState, useCallback, useMemo } from 'react';

import { ChevronDown, ChevronRight, Copy, Check, ExternalLink } from 'lucide-react';

import type { DebugStep } from '@/hooks/useAgentChatStream';
import { cn } from '@/lib/utils';

interface TechnicalDetailsPanelProps {
  debugSteps: DebugStep[];
  /** Execution ID for deep link to Debug Console (Issue #5486) */
  executionId?: string | null;
  className?: string;
}

interface ExtractedMetadata {
  provider: string | null;
  model: string | null;
  promptTokens: number | null;
  completionTokens: number | null;
  totalTokens: number | null;
  confidence: number | null;
  typology: string | null;
  searchStrategy: string | null;
  latencyMs: number | null;
  chunkCount: number | null;
  chunks: Array<{ score: number; page?: number }>;
}

function extractMetadata(steps: DebugStep[]): ExtractedMetadata {
  const meta: ExtractedMetadata = {
    provider: null,
    model: null,
    promptTokens: null,
    completionTokens: null,
    totalTokens: null,
    confidence: null,
    typology: null,
    searchStrategy: null,
    latencyMs: null,
    chunkCount: null,
    chunks: [],
  };

  let totalLatency = 0;

  for (const step of steps) {
    const p = step.payload as Record<string, unknown> | null;
    if (!p) continue;

    switch (step.type) {
      case 17: // DebugCostUpdate
        meta.model = (p.model as string) ?? meta.model;
        meta.promptTokens = (p.promptTokens as number) ?? meta.promptTokens;
        meta.completionTokens = (p.completionTokens as number) ?? meta.completionTokens;
        meta.totalTokens = (p.totalTokens as number) ?? meta.totalTokens;
        meta.confidence = (p.confidence as number) ?? meta.confidence;
        meta.typology = (p.typology as string) ?? meta.typology;
        break;
      case 22: // DebugTypologyProfile
        meta.typology = (p.typology as string) ?? meta.typology;
        meta.searchStrategy = (p.searchStrategy as string) ?? meta.searchStrategy;
        break;
      case 13: // DebugRetrievalResults
        meta.chunkCount = (p.totalResults as number) ?? meta.chunkCount;
        if (Array.isArray(p.results)) {
          meta.chunks = (p.results as Array<{ score: number; page?: number }>)
            .slice(0, 5)
            .map(r => ({ score: r.score, page: r.page }));
        }
        break;
    }

    if (typeof step.latencyMs === 'number') {
      totalLatency += step.latencyMs;
    }
  }

  meta.latencyMs = totalLatency > 0 ? totalLatency : null;

  // Derive provider from model name
  // Ollama models use short names like "llama3:8b", OpenRouter uses "org/model" format
  if (meta.model) {
    meta.provider = meta.model.includes('/') ? 'OpenRouter' : 'Ollama (local)';
  }

  return meta;
}

/**
 * Collapsible technical details panel for Editor/Admin users.
 * Issue #5483: Shows full metadata below AI responses.
 */
export function TechnicalDetailsPanel({ debugSteps, executionId, className }: TechnicalDetailsPanelProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const meta = useMemo(() => extractMetadata(debugSteps), [debugSteps]);

  const hasData = meta.model || meta.totalTokens || meta.typology;

  const handleCopy = useCallback(async () => {
    const text = [
      meta.provider && `Provider: ${meta.provider}`,
      meta.model && `Model: ${meta.model}`,
      meta.promptTokens != null && `Prompt tokens: ${meta.promptTokens}`,
      meta.completionTokens != null && `Completion tokens: ${meta.completionTokens}`,
      meta.totalTokens != null && `Total tokens: ${meta.totalTokens}`,
      meta.latencyMs != null && `Latency: ${meta.latencyMs}ms`,
      meta.confidence != null && `Confidence: ${(meta.confidence * 100).toFixed(0)}%`,
      meta.typology && `Typology: ${meta.typology}`,
      meta.searchStrategy && `Strategy: ${meta.searchStrategy}`,
      meta.chunkCount != null && `Chunks: ${meta.chunkCount}`,
    ]
      .filter(Boolean)
      .join('\n');

    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }, [meta]);

  if (!hasData) return null;

  return (
    <div
      className={cn(
        'mt-2 rounded-lg border border-border/40 bg-muted/30 text-[11px]',
        className,
      )}
      data-testid="technical-details-panel"
    >
      <button
        type="button"
        onClick={() => setIsOpen(prev => !prev)}
        className="flex w-full items-center gap-1.5 px-2.5 py-1.5 text-muted-foreground hover:text-foreground transition-colors"
        aria-expanded={isOpen}
        aria-label="Dettagli tecnici"
        data-testid="technical-details-toggle"
      >
        {isOpen ? (
          <ChevronDown className="h-3 w-3 flex-shrink-0" />
        ) : (
          <ChevronRight className="h-3 w-3 flex-shrink-0" />
        )}
        <span className="font-medium">Dettagli tecnici</span>
        {meta.totalTokens != null && (
          <span className="ml-auto tabular-nums">{meta.totalTokens} token</span>
        )}
      </button>

      {isOpen && (
        <div className="border-t border-border/30 px-2.5 py-2 space-y-1.5">
          <div className="flex items-start justify-between gap-2">
            <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5">
              {meta.provider && (
                <>
                  <dt className="text-muted-foreground">Provider</dt>
                  <dd>{meta.provider}</dd>
                </>
              )}
              {meta.model && (
                <>
                  <dt className="text-muted-foreground">Modello</dt>
                  <dd className="font-mono text-[10px]">{meta.model}</dd>
                </>
              )}
              {meta.promptTokens != null && (
                <>
                  <dt className="text-muted-foreground">Token prompt</dt>
                  <dd className="tabular-nums">{meta.promptTokens}</dd>
                </>
              )}
              {meta.completionTokens != null && (
                <>
                  <dt className="text-muted-foreground">Token risposta</dt>
                  <dd className="tabular-nums">{meta.completionTokens}</dd>
                </>
              )}
              {meta.totalTokens != null && (
                <>
                  <dt className="text-muted-foreground">Token totali</dt>
                  <dd className="tabular-nums font-medium">{meta.totalTokens}</dd>
                </>
              )}
              {meta.latencyMs != null && (
                <>
                  <dt className="text-muted-foreground">Latenza</dt>
                  <dd className="tabular-nums">{meta.latencyMs}ms</dd>
                </>
              )}
              {meta.confidence != null && (
                <>
                  <dt className="text-muted-foreground">Confidenza</dt>
                  <dd className="tabular-nums">{(meta.confidence * 100).toFixed(0)}%</dd>
                </>
              )}
              {meta.typology && (
                <>
                  <dt className="text-muted-foreground">Tipologia</dt>
                  <dd>{meta.typology}</dd>
                </>
              )}
              {meta.searchStrategy && (
                <>
                  <dt className="text-muted-foreground">Strategia</dt>
                  <dd>{meta.searchStrategy}</dd>
                </>
              )}
              {meta.chunkCount != null && (
                <>
                  <dt className="text-muted-foreground">Chunk KB</dt>
                  <dd className="tabular-nums">{meta.chunkCount}</dd>
                </>
              )}
            </dl>

            <button
              type="button"
              onClick={handleCopy}
              className="flex-shrink-0 p-1 rounded hover:bg-muted transition-colors"
              aria-label="Copia dettagli tecnici"
              data-testid="technical-details-copy"
            >
              {copied ? (
                <Check className="h-3 w-3 text-green-500" />
              ) : (
                <Copy className="h-3 w-3 text-muted-foreground" />
              )}
            </button>
          </div>

          {meta.chunks.length > 0 && (
            <div className="pt-1 border-t border-border/20">
              <p className="text-muted-foreground mb-0.5">Chunk rilevanti</p>
              <div className="flex flex-wrap gap-1">
                {meta.chunks.map((chunk, i) => (
                  <span
                    key={i}
                    className="inline-flex items-center gap-0.5 px-1 py-0.5 rounded bg-muted/50 tabular-nums"
                  >
                    {chunk.page != null && <span>p.{chunk.page}</span>}
                    <span className="text-muted-foreground">
                      {(chunk.score * 100).toFixed(0)}%
                    </span>
                  </span>
                ))}
              </div>
            </div>
          )}

          {executionId && (
            <div className="pt-1 border-t border-border/20">
              <a
                href={`/admin/agents/debug-chat?executionId=${executionId}`}
                className="inline-flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
                data-testid="debug-console-link"
              >
                <ExternalLink className="h-3 w-3" />
                Vedi in Debug Console
              </a>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
