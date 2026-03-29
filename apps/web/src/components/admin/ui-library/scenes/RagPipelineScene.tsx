'use client';

import { useState } from 'react';

import { PipelineDiagram } from '@/components/admin/rag/PipelineDiagram';
import type { PipelineStep } from '@/components/admin/rag/PipelineDiagram';
import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';
import { TimelineStep } from '@/components/admin/rag/TimelineStep';
import { WaterfallChart } from '@/components/admin/rag/WaterfallChart';
import type { WaterfallCall } from '@/components/admin/rag/WaterfallChart';
import { ConfidenceBadge } from '@/components/ui/feedback/ConfidenceBadge';

const PIPELINE_STEPS: PipelineStep[] = [
  { name: 'Query', icon: '💬', latencyMs: 8, status: 'ok', color: '#a78bfa' },
  { name: 'Embedding', icon: '🔢', latencyMs: 45, status: 'ok', color: '#60a5fa' },
  { name: 'Search', icon: '🔍', latencyMs: 32, status: 'ok', color: '#34d399' },
  { name: 'Rerank', icon: '⚖️', latencyMs: 28, status: 'ok', color: '#fb923c' },
  { name: 'LLM', icon: '🤖', latencyMs: 312, status: 'ok', color: '#fbbf24' },
  { name: 'Response', icon: '✅', latencyMs: 4, status: 'cache', color: '#6ee7b7' },
];

const WATERFALL_CALLS: WaterfallCall[] = [
  { label: 'Query processing', durationMs: 8, type: 'search', startOffsetMs: 0 },
  { label: 'text-embedding-3-small', durationMs: 45, type: 'embedding', startOffsetMs: 10 },
  { label: 'pgvector hybrid search', durationMs: 32, type: 'search', startOffsetMs: 55 },
  { label: 'cross-encoder rerank', durationMs: 28, type: 'rerank', startOffsetMs: 88 },
  { label: 'openrouter/gpt-4o', durationMs: 312, type: 'llm', startOffsetMs: 116 },
];

export default function RagPipelineScene() {
  const [activeNode, setActiveNode] = useState<number | null>(null);

  return (
    <div className="space-y-6">
      {/* Strategy badges */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Strategy Badges</h3>
        <div className="flex flex-wrap gap-2">
          {['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG', 'RetrievalOnly'].map(s => (
            <StrategyBadge key={s} strategy={s} />
          ))}
        </div>
      </div>

      {/* Confidence badges */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Confidence Badges
        </h3>
        <div className="flex flex-wrap gap-3">
          <ConfidenceBadge confidence={92} />
          <ConfidenceBadge confidence={65} />
          <ConfidenceBadge confidence={31} />
          <ConfidenceBadge confidence={85} size="sm" />
        </div>
      </div>

      {/* Pipeline diagram */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Pipeline Diagram</h3>
        <div className="rounded-xl border border-border/60 bg-muted/20 p-4">
          <PipelineDiagram
            steps={PIPELINE_STEPS}
            activeNodeIndex={activeNode}
            onNodeClick={idx => setActiveNode(prev => (prev === idx ? null : idx))}
          />
        </div>
      </div>

      {/* Timeline steps */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Timeline Steps</h3>
        <div className="space-y-1">
          <TimelineStep
            name="Embedding"
            icon="🔢"
            durationMs={45}
            percentOfTotal={10}
            latencyClass="green"
            details={[
              { label: 'Model', value: 'text-embedding-3-small' },
              { label: 'Dimensions', value: '1536', mono: true },
            ]}
          />
          <TimelineStep
            name="LLM Generation"
            icon="🤖"
            durationMs={312}
            percentOfTotal={73}
            latencyClass="amber"
            isOpen
            details={[
              { label: 'Model', value: 'gpt-4o-mini', badge: 'primary' },
              { label: 'Tokens In', value: '1,248', mono: true },
              { label: 'Tokens Out', value: '312', mono: true },
            ]}
          />
        </div>
      </div>

      {/* Waterfall chart */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Waterfall Chart</h3>
        <div className="rounded-xl border border-border/60 bg-muted/20 p-4">
          <WaterfallChart calls={WATERFALL_CALLS} />
        </div>
      </div>
    </div>
  );
}
