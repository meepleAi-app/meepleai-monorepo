'use client';

import { useState } from 'react';

import { Layers, GitBranch, Timer, Info } from 'lucide-react';

import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Badge } from '@/components/ui/data-display/badge';

import { RetrievedChunkCard } from './RetrievedChunkCard';

import type { RetrievedChunk, PipelineTrace } from './types';

interface DebugSidePanelProps {
  messageData?: {
    chunks: RetrievedChunk[];
    trace: PipelineTrace;
    latencyMs: number;
    avgConfidence: number;
  };
}

export function DebugSidePanel({ messageData }: DebugSidePanelProps) {
  const { appliedConfig } = useSandboxSession();
  const [expandedChunks, setExpandedChunks] = useState<Set<string>>(new Set());

  const toggleChunkExpand = (chunkId: string) => {
    setExpandedChunks(prev => {
      const next = new Set(prev);
      if (next.has(chunkId)) {
        next.delete(chunkId);
      } else {
        next.add(chunkId);
      }
      return next;
    });
  };

  if (!messageData) {
    return (
      <div
        className="flex h-full flex-col items-center justify-center gap-3 p-6 text-muted-foreground"
        data-testid="debug-panel-empty"
      >
        <Info className="h-10 w-10 opacity-30" />
        <p className="font-nunito text-sm text-center">
          Seleziona un messaggio per vedere i dettagli
        </p>
      </div>
    );
  }

  const sortedChunks = [...messageData.chunks].sort((a, b) => b.score - a.score);

  return (
    <div className="flex h-full flex-col" data-testid="debug-side-panel">
      {/* Accordion sections */}
      <div className="flex-1 overflow-y-auto p-3">
        <Accordion type="multiple" defaultValue={['chunks']}>
          {/* Chunk Recuperati */}
          <AccordionItem value="chunks">
            <AccordionTrigger className="text-sm font-quicksand" data-testid="accordion-chunks">
              <span className="flex items-center gap-2">
                <Layers className="h-4 w-4 text-amber-600" />
                Chunk Recuperati
                <Badge variant="outline" className="ml-1 text-[10px] px-1.5 py-0">
                  {sortedChunks.length}
                </Badge>
              </span>
            </AccordionTrigger>
            <AccordionContent>
              <div className="space-y-2" data-testid="chunks-list">
                {sortedChunks.map(chunk => (
                  <RetrievedChunkCard
                    key={chunk.id}
                    chunk={chunk}
                    expanded={expandedChunks.has(chunk.id)}
                    onToggleExpand={() => toggleChunkExpand(chunk.id)}
                  />
                ))}
              </div>
            </AccordionContent>
          </AccordionItem>

          {/* Traccia Pipeline */}
          <AccordionItem value="trace">
            <AccordionTrigger className="text-sm font-quicksand" data-testid="accordion-trace">
              <span className="flex items-center gap-2">
                <GitBranch className="h-4 w-4 text-amber-600" />
                Traccia Pipeline
              </span>
            </AccordionTrigger>
            <AccordionContent>
              {/* Lazy import to avoid circular — inline for now */}
              <div data-testid="trace-section">
                {messageData.trace.steps.map((step, i) => (
                  <div
                    key={`${step.name}-${i}`}
                    className="flex items-center justify-between px-2 py-1 text-xs font-nunito"
                  >
                    <span>{step.name}</span>
                    <span className="tabular-nums text-muted-foreground">{step.durationMs}ms</span>
                  </div>
                ))}
                <div className="border-t mt-1 pt-1 flex items-center justify-between px-2 text-xs font-nunito font-semibold">
                  <span>Totale</span>
                  <span className="tabular-nums">{messageData.trace.totalDurationMs}ms</span>
                </div>
              </div>
            </AccordionContent>
          </AccordionItem>

          {/* Waterfall Tempi */}
          <AccordionItem value="waterfall">
            <AccordionTrigger className="text-sm font-quicksand" data-testid="accordion-waterfall">
              <span className="flex items-center gap-2">
                <Timer className="h-4 w-4 text-amber-600" />
                Waterfall Tempi
              </span>
            </AccordionTrigger>
            <AccordionContent>
              <div
                className="flex items-center justify-center py-6 text-xs text-muted-foreground font-nunito"
                data-testid="waterfall-placeholder"
              >
                Waterfall chart — coming soon
              </div>
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      </div>

      {/* Footer summary bar */}
      <div
        className="shrink-0 border-t bg-white/50 px-3 py-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs font-nunito"
        data-testid="debug-footer"
      >
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Strategia</span>
          <span className="font-medium">{appliedConfig.strategy}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Reranking</span>
          <Badge
            variant="outline"
            className={`text-[10px] px-1.5 py-0 ${
              appliedConfig.reranking
                ? 'border-green-300 text-green-700'
                : 'border-gray-300 text-gray-500'
            }`}
          >
            {appliedConfig.reranking ? 'On' : 'Off'}
          </Badge>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Confidenza</span>
          <span className="font-medium tabular-nums">
            {(messageData.avgConfidence * 100).toFixed(0)}%
          </span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-muted-foreground">Latenza</span>
          <span className="font-medium tabular-nums">{messageData.latencyMs}ms</span>
        </div>
      </div>
    </div>
  );
}
