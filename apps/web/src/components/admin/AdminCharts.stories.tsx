import type { Meta, StoryObj } from '@storybook/react';
import React from 'react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
} from './AdminCharts';

const meta: Meta = {
  title: 'Admin/Charts/Dashboard',
  parameters: {
    layout: 'fullscreen',
    chromatic: { disableSnapshot: false },
  },
};

export default meta;
type Story = StoryObj;

const endpointCounts = { qa: 14, explain: 6, setup: 3, chess: 2, other: 1 };
const latencySamples = [
  { latencyMs: 42, endpoint: 'qa', createdAt: '2025-12-10T08:00:00Z' },
  { latencyMs: 130, endpoint: 'explain', createdAt: '2025-12-10T08:01:00Z' },
  { latencyMs: 260, endpoint: 'setup', createdAt: '2025-12-10T08:02:00Z' },
  { latencyMs: 480, endpoint: 'qa', createdAt: '2025-12-10T08:03:00Z' },
  { latencyMs: 720, endpoint: 'chess', createdAt: '2025-12-10T08:04:00Z' },
];
const requestHistory = [
  { createdAt: '2025-12-10T08:05:00Z', status: 'Success' },
  { createdAt: '2025-12-10T08:15:00Z', status: 'Error' },
  { createdAt: '2025-12-10T09:00:00Z', status: 'Success' },
  { createdAt: '2025-12-10T09:20:00Z', status: 'Success' },
];
const feedbackCounts = { helpful: 18, notHelpful: 4 };

export const Default: Story = {
  name: 'Dashboard charts',
  render: () => (
    <div className="space-y-8 p-6 bg-slate-50">
      <EndpointDistributionChart endpointCounts={endpointCounts} />
      <LatencyDistributionChart requests={latencySamples} />
      <RequestsTimeSeriesChart requests={requestHistory} />
      <FeedbackChart feedbackCounts={feedbackCounts} />
    </div>
  ),
};

export const EmptyStates: Story = {
  name: 'Empty data',
  render: () => (
    <div className="space-y-8 p-6 bg-slate-50">
      <EndpointDistributionChart endpointCounts={{}} />
      <LatencyDistributionChart requests={[]} />
      <RequestsTimeSeriesChart requests={[]} />
      <FeedbackChart feedbackCounts={{ helpful: 0, notHelpful: 0 }} />
    </div>
  ),
};

export const LoadingStates: Story = {
  name: 'Loading placeholders',
  render: () => (
    <div className="space-y-8 p-6 bg-slate-50">
      <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100" />
      <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100" />
      <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100" />
      <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100" />
    </div>
  ),
};

export const ErrorStates: Story = {
  name: 'Error placeholders',
  render: () => (
    <div className="space-y-4 p-6 bg-slate-50">
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800">
        Failed to load charts. Please retry.
      </div>
      <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-4 text-yellow-800">
        Metrics temporarily unavailable.
      </div>
    </div>
  ),
};
