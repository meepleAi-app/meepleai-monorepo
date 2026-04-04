/**
 * StrategyBadge Story
 * Demonstrates the RAG strategy color-coded badge.
 */

'use client';

import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';

import type { ShowcaseStory } from '../types';

type StrategyBadgeShowcaseProps = {
  strategy: string;
};

export const strategyBadgeStory: ShowcaseStory<StrategyBadgeShowcaseProps> = {
  id: 'strategy-badge',
  title: 'StrategyBadge',
  category: 'Agent',
  description: 'Color-coded badge for RAG strategy types used in the admin pipeline view.',

  component: function StrategyBadgeStory({ strategy }: StrategyBadgeShowcaseProps) {
    return (
      <div className="flex flex-wrap items-center gap-3 p-6">
        <StrategyBadge strategy={strategy} />
        <div className="flex flex-wrap gap-2 pt-3 w-full border-t border-border/30">
          {['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG', 'RetrievalOnly'].map(s => (
            <StrategyBadge key={s} strategy={s} />
          ))}
        </div>
      </div>
    );
  },

  defaultProps: {
    strategy: 'HybridRAG',
  },

  controls: {
    strategy: {
      type: 'select',
      label: 'strategy',
      options: ['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG', 'RetrievalOnly'],
      default: 'HybridRAG',
    },
  },

  presets: {
    poc: { label: 'POC', props: { strategy: 'POC' } },
    hybrid: { label: 'HybridRAG', props: { strategy: 'HybridRAG' } },
    multi: { label: 'MultiModel', props: { strategy: 'MultiModelConsensus' } },
  },
};
