/**
 * ProgressBadge Storybook Stories (Issue #4210)
 */

import { ProgressBadge } from './progress-badge';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'PDF/ProgressBadge',
  component: ProgressBadge,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ProgressBadge>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Pending state */
export const Pending: Story = {
  args: {
    state: 'pending',
    progress: 0,
  },
};

/** Uploading state with amber color and pulse */
export const Uploading: Story = {
  args: {
    state: 'uploading',
    progress: 20,
    eta: '00:08:00',
  },
};

/** Extracting state (blue processing) */
export const Extracting: Story = {
  args: {
    state: 'extracting',
    progress: 35,
    eta: '00:05:30',
  },
};

/** Chunking state (blue processing) */
export const Chunking: Story = {
  args: {
    state: 'chunking',
    progress: 50,
    eta: '00:04:00',
  },
};

/** Embedding state (blue processing) */
export const Embedding: Story = {
  args: {
    state: 'embedding',
    progress: 75,
    eta: '00:02:00',
  },
};

/** Indexing state (blue processing) */
export const Indexing: Story = {
  args: {
    state: 'indexing',
    progress: 90,
    eta: '00:00:45',
  },
};

/** Ready state (green checkmark) */
export const Ready: Story = {
  args: {
    state: 'ready',
    progress: 100,
  },
};

/** Failed state (red X) */
export const Failed: Story = {
  args: {
    state: 'failed',
    progress: 40,
  },
};

/** Multiple badges in grid (all states) */
export const AllStates: Story = {
  render: () => (
    <div className="grid grid-cols-4 gap-4 p-4">
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="pending" progress={0} />
        <span className="text-xs">Pending</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="uploading" progress={20} eta="00:08:00" />
        <span className="text-xs">Uploading</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="extracting" progress={35} eta="00:05:30" />
        <span className="text-xs">Extracting</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="chunking" progress={50} eta="00:04:00" />
        <span className="text-xs">Chunking</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="embedding" progress={75} eta="00:02:00" />
        <span className="text-xs">Embedding</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="indexing" progress={90} eta="00:00:45" />
        <span className="text-xs">Indexing</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="ready" progress={100} />
        <span className="text-xs">Ready</span>
      </div>
      <div className="flex flex-col items-center gap-2">
        <ProgressBadge state="failed" progress={40} />
        <span className="text-xs">Failed</span>
      </div>
    </div>
  ),
};
