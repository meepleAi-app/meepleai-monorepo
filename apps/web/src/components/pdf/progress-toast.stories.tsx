/**
 * ProgressToast Storybook Stories (Issue #4210)
 */

import { fn } from 'storybook/test';
import { vi } from 'vitest';

import { usePdfProgress } from '@/hooks/usePdfProgress';

import { ProgressToast } from './progress-toast';

import type { Meta, StoryObj } from '@storybook/react';


vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(),
}));

const mockScenarios: Record<string, any> = {
  uploading: {
    status: { state: 'uploading', progress: 25, timestamp: new Date().toISOString() },
    metrics: { progressPercentage: 25 },
  },
  processing: {
    status: { state: 'embedding', progress: 70, timestamp: new Date().toISOString() },
    metrics: { progressPercentage: 70 },
  },
  complete: {
    status: { state: 'ready', progress: 100, timestamp: new Date().toISOString() },
    metrics: { progressPercentage: 100 },
  },
  failed: {
    status: { state: 'failed', progress: 40, errorMessage: 'Processing failed', timestamp: new Date().toISOString() },
    metrics: { progressPercentage: 40 },
  },
};

const meta = {
  title: 'PDF/ProgressToast',
  component: ProgressToast,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ProgressToast>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Uploading: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.uploading);
    return <ProgressToast {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Game Manual.pdf',
    onViewDetails: fn(),
    onDismiss: fn(),
  },
};

export const Processing: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.processing);
    return <ProgressToast {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Strategy Guide.pdf',
    onViewDetails: fn(),
    onDismiss: fn(),
  },
};

export const Complete: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.complete);
    return <ProgressToast {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Rules.pdf',
    onViewDetails: fn(),
    onDismiss: fn(),
  },
};

export const Failed: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.failed);
    return <ProgressToast {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Corrupted.pdf',
    onViewDetails: fn(),
    onDismiss: fn(),
  },
};

export const WithoutViewDetails: Story = {
  render: (args) => {
    vi.mocked(usePdfProgress).mockReturnValue(mockScenarios.processing);
    return <ProgressToast {...args} />;
  },
  args: {
    documentId: 'doc-123',
    title: 'Document.pdf',
    onDismiss: fn(),
  },
};
