/**
 * ProgressCard Storybook Stories (Issue #4210)
 */

import { fn } from 'storybook/test';
import { vi } from 'vitest';

import { usePdfProgress } from '@/hooks/usePdfProgress';

import { ProgressCard } from './progress-card';

import type { Meta, StoryObj } from '@storybook/react';


// Mock hook
vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(),
}));

const mockScenarios: Record<string, any> = {
  uploading: {
    status: { state: 'uploading', progress: 20, timestamp: new Date().toISOString() },
    metrics: { documentId: 'doc-1', currentState: 'Uploading', progressPercentage: 20, pageCount: null, estimatedTimeRemaining: '00:07:30', totalDuration: '00:02:00', stateDurations: {}, retryCount: 0 },
    isPolling: false,
  },
  processing: {
    status: { state: 'chunking', progress: 55, timestamp: new Date().toISOString() },
    metrics: { documentId: 'doc-1', currentState: 'Chunking', progressPercentage: 55, pageCount: 28, estimatedTimeRemaining: '00:03:20', totalDuration: '00:04:10', stateDurations: {}, retryCount: 0 },
    isPolling: false,
  },
  ready: {
    status: { state: 'ready', progress: 100, timestamp: new Date().toISOString() },
    metrics: { documentId: 'doc-1', currentState: 'Completed', progressPercentage: 100, pageCount: 28, estimatedTimeRemaining: null, totalDuration: '00:07:30', stateDurations: {}, retryCount: 0 },
    isPolling: false,
  },
  failed: {
    status: { state: 'failed', progress: 40, errorMessage: 'PDF extraction failed', timestamp: new Date().toISOString() },
    metrics: { documentId: 'doc-1', currentState: 'Failed', progressPercentage: 40, pageCount: null, estimatedTimeRemaining: null, totalDuration: '00:03:00', stateDurations: {}, retryCount: 2 },
    isPolling: false,
  },
  polling: {
    status: { state: 'embedding', progress: 70, timestamp: new Date().toISOString() },
    metrics: { documentId: 'doc-1', currentState: 'Embedding', progressPercentage: 70, pageCount: 28, estimatedTimeRemaining: '00:02:00', totalDuration: '00:05:20', stateDurations: {}, retryCount: 0 },
    isPolling: true,
  },
};

const meta = {
  title: 'PDF/ProgressCard',
  component: ProgressCard,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ProgressCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Uploading state - 20% progress */
export const Uploading: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.uploading);
    return <ProgressCard {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Game Rules.pdf',
    subtitle: 'Uploaded 2 minutes ago',
    onViewDetails: fn(),
  },
};

/** Processing state - 55% progress with expanded details */
export const Processing: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.processing);
    return <ProgressCard {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Player Guide.pdf',
    subtitle: '2.4 MB',
    defaultExpanded: true,
    onViewDetails: fn(),
  },
};

/** Ready state - 100% complete */
export const Ready: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.ready);
    return <ProgressCard {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Strategy Manual.pdf',
    subtitle: 'Completed successfully',
    onViewDetails: fn(),
  },
};

/** Failed state with error message */
export const Failed: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.failed);
    return <ProgressCard {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Corrupted.pdf',
    subtitle: 'Failed 1 minute ago',
    defaultExpanded: true,
    onViewDetails: fn(),
  },
};

/** Polling fallback indicator */
export const PollingFallback: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.polling);
    return <ProgressCard {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Reference.pdf',
    subtitle: '1.8 MB',
    onViewDetails: fn(),
  },
};

/** Multiple cards in list */
export const MultipleCards: Story = {
  render: () => {
    return (
      <div className="space-y-4 w-[400px]">
        {Object.keys(mockScenarios).map((scenario, i) => {
          vi.mocked(usePdfProgress).mockReturnValue(mockScenarios[scenario]);
          return (
            <ProgressCard
              key={i}
              documentId={`doc-${i}`}
              title={`Document ${i + 1}.pdf`}
              subtitle={scenario}
              onViewDetails={fn()}
            />
          );
        })}
      </div>
    );
  },
};
