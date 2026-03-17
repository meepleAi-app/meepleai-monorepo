/**
 * WaterfallChart Story
 * Demonstrates the RAG pipeline timing waterfall chart.
 */

'use client';

import { WaterfallChart } from '@/components/admin/rag/WaterfallChart';
import type { WaterfallCall } from '@/components/admin/rag/WaterfallChart';

import type { ShowcaseStory } from '../types';

type WaterfallChartShowcaseProps = {
  scenario: string;
};

const SCENARIOS: Record<string, WaterfallCall[]> = {
  typical: [
    { label: 'Embedding', durationMs: 45, type: 'embedding', startOffsetMs: 0 },
    { label: 'Vector Search', durationMs: 23, type: 'search', startOffsetMs: 45 },
    { label: 'Rerank', durationMs: 67, type: 'rerank', startOffsetMs: 68 },
    { label: 'LLM Generate', durationMs: 840, type: 'llm', startOffsetMs: 135 },
  ],
  cached: [
    { label: 'Cache Lookup', durationMs: 3, type: 'cache', startOffsetMs: 0 },
    { label: 'LLM Generate', durationMs: 210, type: 'llm', startOffsetMs: 3 },
  ],
  heavy: [
    { label: 'Embedding', durationMs: 112, type: 'embedding', startOffsetMs: 0 },
    { label: 'Vector Search', durationMs: 89, type: 'search', startOffsetMs: 112 },
    { label: 'Rerank Top-20', durationMs: 234, type: 'rerank', startOffsetMs: 201 },
    { label: 'LLM (GPT-4)', durationMs: 3200, type: 'llm', startOffsetMs: 435 },
  ],
};

export const waterfallChartStory: ShowcaseStory<WaterfallChartShowcaseProps> = {
  id: 'waterfall-chart',
  title: 'WaterfallChart',
  category: 'Charts',
  description: 'Chrome DevTools-style waterfall for visualizing RAG pipeline timing.',

  component: function WaterfallChartStory({ scenario }: WaterfallChartShowcaseProps) {
    const calls = SCENARIOS[scenario] ?? SCENARIOS.typical;
    return (
      <div className="w-full max-w-2xl p-6">
        <WaterfallChart calls={calls} />
      </div>
    );
  },

  defaultProps: {
    scenario: 'typical',
  },

  controls: {
    scenario: {
      type: 'select',
      label: 'scenario',
      options: ['typical', 'cached', 'heavy'],
      default: 'typical',
    },
  },

  presets: {
    typical: { label: 'Typical RAG', props: { scenario: 'typical' } },
    cached: { label: 'Cache Hit', props: { scenario: 'cached' } },
    heavy: { label: 'Heavy Query', props: { scenario: 'heavy' } },
  },
};
