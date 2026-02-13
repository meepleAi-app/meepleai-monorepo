import type { Meta, StoryObj } from '@storybook/react';
import { PdfStatusTimeline } from './PdfStatusTimeline';

const meta: Meta<typeof PdfStatusTimeline> = {
  title: 'Components/PDF/PdfStatusTimeline',
  component: PdfStatusTimeline,
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof PdfStatusTimeline>;

export const JustStarted: Story = {
  args: {
    currentState: 'uploading',
    completedStates: [],
    startedAt: new Date().toISOString(),
    estimatedCompletion: new Date(Date.now() + 120000).toISOString(),
  },
};

export const MidProgress: Story = {
  args: {
    currentState: 'embedding',
    completedStates: ['uploading', 'extracting', 'chunking'],
    startedAt: new Date(Date.now() - 60000).toISOString(),
    estimatedCompletion: new Date(Date.now() + 60000).toISOString(),
  },
};

export const Completed: Story = {
  args: {
    currentState: 'ready',
    completedStates: ['uploading', 'extracting', 'chunking', 'embedding', 'indexing'],
    startedAt: new Date(Date.now() - 180000).toISOString(),
  },
};

export const Failed: Story = {
  args: {
    currentState: 'failed',
    completedStates: ['uploading', 'extracting'],
    startedAt: new Date(Date.now() - 90000).toISOString(),
  },
};
