/**
 * KpiCards Story
 * Demonstrates the admin OpenRouter KPI cards with mock data.
 */

'use client';

import { KpiCards } from '@/components/admin/usage/KpiCards';

import type { ShowcaseStory } from '../types';

type KpiCardsShowcaseProps = {
  isLoading: boolean;
  isThrottled: boolean;
  spend: number;
  requests: number;
};

export const kpiCardsStory: ShowcaseStory<KpiCardsShowcaseProps> = {
  id: 'kpi-cards',
  title: 'KpiCards',
  category: 'Charts',
  description: 'Admin dashboard KPI grid showing spend, requests, RPM utilization, and balance.',

  component: function KpiCardsStory({
    isLoading,
    isThrottled,
    spend,
    requests,
  }: KpiCardsShowcaseProps) {
    const status = isLoading
      ? null
      : {
          balanceUsd: 8.42,
          dailySpendUsd: spend,
          todayRequestCount: requests,
          currentRpm: isThrottled ? 58 : 12,
          limitRpm: 60,
          utilizationPercent: isThrottled ? 96 : 20,
          isThrottled,
          isFreeTier: false,
          rateLimitInterval: '1m',
          lastUpdated: new Date().toISOString(),
        };

    return (
      <div className="w-full max-w-3xl p-6">
        <KpiCards status={status} isLoading={isLoading} />
      </div>
    );
  },

  defaultProps: {
    isLoading: false,
    isThrottled: false,
    spend: 0.0145,
    requests: 42,
  },

  controls: {
    isLoading: { type: 'boolean', label: 'isLoading', default: false },
    isThrottled: { type: 'boolean', label: 'isThrottled', default: false },
    spend: { type: 'range', label: 'spend ($)', min: 0, max: 5, step: 0.01, default: 0.0145 },
    requests: { type: 'range', label: 'requests', min: 0, max: 500, step: 1, default: 42 },
  },

  presets: {
    normal: {
      label: 'Normal',
      props: { isLoading: false, isThrottled: false, spend: 0.014, requests: 42 },
    },
    throttled: { label: 'Throttled', props: { isThrottled: true, spend: 2.3, requests: 380 } },
    loading: { label: 'Loading', props: { isLoading: true } },
  },
};
