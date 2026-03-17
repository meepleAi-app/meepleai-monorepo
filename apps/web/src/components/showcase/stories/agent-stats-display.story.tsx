/**
 * AgentStatsDisplay Story
 * Demonstrates agent metadata display with status, stats, and capabilities.
 */

'use client';

import { AgentStatsDisplay } from '@/components/ui/agent/AgentStatsDisplay';

import type { ShowcaseStory } from '../types';

type AgentStatsShowcaseProps = {
  status: string;
  invocationCount: number;
  avgResponseTime: number;
  capabilities: string;
};

const CAPABILITY_SETS: Record<
  string,
  Array<'RAG' | 'Vision' | 'Code' | 'Functions' | 'MultiTurn'>
> = {
  rag: ['RAG', 'MultiTurn'],
  full: ['RAG', 'Vision', 'Code', 'Functions', 'MultiTurn'],
  minimal: ['RAG'],
  code: ['Code', 'Functions'],
};

export const agentStatsDisplayStory: ShowcaseStory<AgentStatsShowcaseProps> = {
  id: 'agent-stats-display',
  title: 'AgentStatsDisplay',
  category: 'Agent',
  description:
    'Agent metadata panel with status badge, invocation count, response time, and capabilities.',

  component: function AgentStatsDisplayStory({
    status,
    invocationCount,
    avgResponseTime,
    capabilities,
  }: AgentStatsShowcaseProps) {
    const caps = CAPABILITY_SETS[capabilities] ?? CAPABILITY_SETS.rag;

    return (
      <div className="p-6 max-w-xs">
        <AgentStatsDisplay
          metadata={{
            status: status as 'active' | 'idle' | 'training' | 'error',
            model: { name: 'claude-sonnet-4-5', temperature: 0.7, maxTokens: 2048 },
            invocationCount,
            lastExecuted: new Date(Date.now() - 3600000).toISOString(),
            avgResponseTime,
            capabilities: caps,
          }}
        />
      </div>
    );
  },

  defaultProps: {
    status: 'active',
    invocationCount: 1247,
    avgResponseTime: 842,
    capabilities: 'rag',
  },

  controls: {
    status: {
      type: 'select',
      label: 'status',
      options: ['active', 'idle', 'training', 'error'],
      default: 'active',
    },
    invocationCount: {
      type: 'range',
      label: 'invocationCount',
      min: 0,
      max: 10000,
      step: 100,
      default: 1247,
    },
    avgResponseTime: {
      type: 'range',
      label: 'avgResponseTime (ms)',
      min: 100,
      max: 5000,
      step: 100,
      default: 842,
    },
    capabilities: {
      type: 'select',
      label: 'capabilities',
      options: ['rag', 'full', 'minimal', 'code'],
      default: 'rag',
    },
  },

  presets: {
    active: {
      label: 'Active RAG',
      props: { status: 'active', invocationCount: 1247, capabilities: 'rag' },
    },
    training: {
      label: 'Training',
      props: { status: 'training', invocationCount: 0, capabilities: 'full' },
    },
    error: {
      label: 'Error',
      props: { status: 'error', invocationCount: 342, capabilities: 'minimal' },
    },
    heavy: {
      label: 'Full Caps',
      props: { status: 'active', capabilities: 'full', invocationCount: 5400 },
    },
  },
};
