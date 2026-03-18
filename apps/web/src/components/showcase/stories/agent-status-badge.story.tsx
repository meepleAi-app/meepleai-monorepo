/**
 * AgentStatusBadge Story
 * Demonstrates all agent status states with label toggle.
 */

'use client';

import { AgentStatusBadge } from '@/components/ui/agent/AgentStatusBadge';

import type { ShowcaseStory } from '../types';

type AgentStatusBadgeShowcaseProps = {
  status: string;
  showLabel: boolean;
};

export const agentStatusBadgeStory: ShowcaseStory<AgentStatusBadgeShowcaseProps> = {
  id: 'agent-status-badge',
  title: 'AgentStatusBadge',
  category: 'Agent',
  description: 'Color-coded badge indicating agent operational status with optional label.',

  component: function AgentStatusBadgeStory({ status, showLabel }: AgentStatusBadgeShowcaseProps) {
    return (
      <div className="flex items-center gap-4 p-6">
        <AgentStatusBadge
          status={status as 'active' | 'idle' | 'training' | 'error'}
          showLabel={showLabel}
        />
      </div>
    );
  },

  defaultProps: {
    status: 'active',
    showLabel: true,
  },

  controls: {
    status: {
      type: 'select',
      label: 'status',
      options: ['active', 'idle', 'training', 'error'],
      default: 'active',
    },
    showLabel: { type: 'boolean', label: 'showLabel', default: true },
  },

  presets: {
    active: { label: 'Active', props: { status: 'active' } },
    idle: { label: 'Idle', props: { status: 'idle' } },
    training: { label: 'Training', props: { status: 'training' } },
    error: { label: 'Error', props: { status: 'error' } },
  },
};
