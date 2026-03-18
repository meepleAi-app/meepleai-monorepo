/**
 * StatCard Story
 * Demonstrates the StatCard metric display with trend, variant, and loading states.
 */

'use client';

import { Users } from 'lucide-react';

import { StatCard } from '@/components/ui/data-display/stat-card';

import type { ShowcaseStory } from '../types';

type StatCardShowcaseProps = {
  label: string;
  value: string;
  trend: string;
  trendValue: string;
  variant: string;
  loading: boolean;
};

export const statCardStory: ShowcaseStory<StatCardShowcaseProps> = {
  id: 'stat-card',
  title: 'StatCard',
  category: 'Data Display',
  description: 'Metric card with icon, value, trend indicator, and semantic color variants.',

  component: function StatCardStory({
    label,
    value,
    trend,
    trendValue,
    variant,
    loading,
  }: StatCardShowcaseProps) {
    return (
      <div className="w-64 p-4">
        <StatCard
          label={label}
          value={value}
          icon={Users}
          trend={trend as 'up' | 'down' | 'neutral' | undefined}
          trendValue={trendValue || undefined}
          variant={variant as 'default' | 'success' | 'warning' | 'danger'}
          loading={loading}
        />
      </div>
    );
  },

  defaultProps: {
    label: 'Total Users',
    value: '1,247',
    trend: 'up',
    trendValue: '+12% this week',
    variant: 'default',
    loading: false,
  },

  controls: {
    label: { type: 'text', label: 'label', default: 'Total Users' },
    value: { type: 'text', label: 'value', default: '1,247' },
    trend: {
      type: 'select',
      label: 'trend',
      options: ['up', 'down', 'neutral'],
      default: 'up',
    },
    trendValue: { type: 'text', label: 'trendValue', default: '+12% this week' },
    variant: {
      type: 'select',
      label: 'variant',
      options: ['default', 'success', 'warning', 'danger'],
      default: 'default',
    },
    loading: { type: 'boolean', label: 'loading', default: false },
  },

  presets: {
    users: {
      label: 'Users',
      props: {
        label: 'Total Users',
        value: '1,247',
        trend: 'up',
        trendValue: '+12% this week',
        variant: 'success',
      },
    },
    sessions: {
      label: 'Sessions',
      props: {
        label: 'Active Sessions',
        value: '42',
        trend: 'neutral',
        trendValue: 'No change',
        variant: 'default',
      },
    },
    errors: {
      label: 'Errors',
      props: {
        label: 'API Errors',
        value: '8',
        trend: 'down',
        trendValue: '-3 today',
        variant: 'danger',
      },
    },
    loading: { label: 'Loading', props: { loading: true } },
  },
};
