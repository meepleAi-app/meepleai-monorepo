/**
 * ProgressModal Storybook Stories (Issue #4210)
 *
 * Visual documentation for PDF progress modal component.
 */

import { useState } from 'react';

import { fn } from 'storybook/test';

import { Button } from '@/components/ui/primitives/button';

import { ProgressModal } from './progress-modal';

import type { Meta, StoryObj } from '@storybook/react';

// ============================================================================
// Mock Hook
// ============================================================================

const mockUsePdfProgress = (scenario: string) => {
  const scenarios: Record<string, any> = {
    uploading: {
      status: {
        state: 'uploading',
        progress: 15,
        eta: '8m 30s',
        timestamp: new Date().toISOString(),
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Uploading',
        progressPercentage: 15,
        totalDuration: '00:01:30',
        estimatedTimeRemaining: '00:08:30',
        stateDurations: { Uploading: '00:01:30' },
        retryCount: 0,
        pageCount: null,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    extracting: {
      status: {
        state: 'extracting',
        progress: 25,
        eta: '6m 15s',
        timestamp: new Date().toISOString(),
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Extracting',
        progressPercentage: 25,
        totalDuration: '00:02:45',
        estimatedTimeRemaining: '00:06:15',
        stateDurations: { Uploading: '00:01:30', Extracting: '00:01:15' },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    chunking: {
      status: {
        state: 'chunking',
        progress: 50,
        eta: '4m 20s',
        timestamp: new Date().toISOString(),
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Chunking',
        progressPercentage: 50,
        totalDuration: '00:04:15',
        estimatedTimeRemaining: '00:04:20',
        stateDurations: { Uploading: '00:01:30', Extracting: '00:01:15', Chunking: '00:01:30' },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    embedding: {
      status: {
        state: 'embedding',
        progress: 75,
        eta: '2m 10s',
        timestamp: new Date().toISOString(),
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Embedding',
        progressPercentage: 75,
        totalDuration: '00:06:30',
        estimatedTimeRemaining: '00:02:10',
        stateDurations: {
          Uploading: '00:01:30',
          Extracting: '00:01:15',
          Chunking: '00:01:30',
          Embedding: '00:02:15',
        },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    indexing: {
      status: { state: 'indexing', progress: 90, eta: '45s', timestamp: new Date().toISOString() },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Indexing',
        progressPercentage: 90,
        totalDuration: '00:08:15',
        estimatedTimeRemaining: '00:00:45',
        stateDurations: {
          Uploading: '00:01:30',
          Extracting: '00:01:15',
          Chunking: '00:01:30',
          Embedding: '00:02:15',
          Indexing: '00:01:45',
        },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    complete: {
      status: { state: 'ready', progress: 100, eta: null, timestamp: new Date().toISOString() },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Completed',
        progressPercentage: 100,
        totalDuration: '00:09:00',
        estimatedTimeRemaining: null,
        stateDurations: {
          Uploading: '00:01:30',
          Extracting: '00:01:15',
          Chunking: '00:01:30',
          Embedding: '00:02:15',
          Indexing: '00:02:30',
        },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    failed: {
      status: {
        state: 'failed',
        progress: 35,
        eta: null,
        timestamp: new Date().toISOString(),
        errorMessage: 'PDF extraction failed: Corrupted file format',
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Failed',
        progressPercentage: 35,
        totalDuration: '00:03:20',
        estimatedTimeRemaining: null,
        stateDurations: { Uploading: '00:01:30', Extracting: '00:01:50' },
        retryCount: 2,
        pageCount: null,
      },
      isConnected: true,
      isPolling: false,
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
    polling: {
      status: {
        state: 'chunking',
        progress: 45,
        eta: '5m 0s',
        timestamp: new Date().toISOString(),
      },
      metrics: {
        documentId: '123e4567-e89b-12d3-a456-426614174000',
        currentState: 'Chunking',
        progressPercentage: 45,
        totalDuration: '00:04:00',
        estimatedTimeRemaining: '00:05:00',
        stateDurations: { Uploading: '00:01:30', Extracting: '00:01:15', Chunking: '00:01:15' },
        retryCount: 0,
        pageCount: 42,
      },
      isConnected: false,
      isPolling: true, // Polling fallback
      isLoading: false,
      error: null,
      metricsError: null,
      metricsLoading: false,
      reconnect: fn(),
      refreshMetrics: fn(),
    },
  };

  return scenarios[scenario] || scenarios.uploading;
};

// Mock the hook
vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(),
}));

import { usePdfProgress } from '@/hooks/usePdfProgress';

import { vi } from 'vitest';

// ============================================================================
// Meta Configuration
// ============================================================================

const meta = {
  title: 'PDF/ProgressModal',
  component: ProgressModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Full-screen modal for PDF upload progress with real-time SSE updates, step indicators, and metrics display.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    documentId: {
      control: 'text',
      description: 'Document ID for tracking progress',
    },
    isOpen: {
      control: 'boolean',
      description: 'Whether modal is open',
    },
    title: {
      control: 'text',
      description: 'Custom modal title',
    },
    hideCancelButton: {
      control: 'boolean',
      description: 'Hide cancel button',
    },
  },
} satisfies Meta<typeof ProgressModal>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Interactive Wrapper
// ============================================================================

function InteractiveWrapper({
  scenario,
  ...props
}: {
  scenario: string;
  documentId?: string;
  title?: string;
  hideCancelButton?: boolean;
}) {
  const [isOpen, setIsOpen] = useState(true);

  // Mock hook for this scenario
  vi.mocked(usePdfProgress).mockReturnValue(mockUsePdfProgress(scenario));

  return (
    <div>
      <Button onClick={() => setIsOpen(true)}>Open Modal</Button>
      <ProgressModal
        {...props}
        documentId={props.documentId || 'doc-123'}
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        onCancel={async () => {
          await new Promise(resolve => setTimeout(resolve, 1000));
          console.log('Processing canceled');
        }}
      />
    </div>
  );
}

// ============================================================================
// Stories
// ============================================================================

/**
 * Default state - Uploading at 15%
 * Initial stage with minimal metrics.
 */
export const Default: Story = {
  render: args => <InteractiveWrapper scenario="uploading" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Extracting state at 25%
 * Shows 42 pages being processed.
 */
export const Extracting: Story = {
  render: args => <InteractiveWrapper scenario="extracting" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Chunking state at 50%
 * Halfway through processing with full metrics.
 */
export const Chunking: Story = {
  render: args => <InteractiveWrapper scenario="chunking" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Embedding state at 75%
 * Nearly complete with accurate ETA (2m 10s).
 */
export const Embedding: Story = {
  render: args => <InteractiveWrapper scenario="embedding" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Indexing state at 90%
 * Final stage before completion.
 */
export const Indexing: Story = {
  render: args => <InteractiveWrapper scenario="indexing" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Complete state at 100%
 * Processing finished successfully.
 * Shows completion checkmark and close button.
 */
export const Complete: Story = {
  render: args => <InteractiveWrapper scenario="complete" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Failed state
 * Processing error with error message display.
 * Shows error icon and detailed error message.
 */
export const Failed: Story = {
  render: args => <InteractiveWrapper scenario="failed" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Polling fallback indicator
 * Shows ⚠️ indicator when SSE connection lost and using polling.
 */
export const PollingFallback: Story = {
  render: args => <InteractiveWrapper scenario="polling" {...args} />,
  args: {
    documentId: 'doc-123',
  },
};

/**
 * Custom title
 * Modal with custom title text.
 */
export const CustomTitle: Story = {
  render: args => <InteractiveWrapper scenario="chunking" {...args} />,
  args: {
    documentId: 'doc-123',
    title: 'Analyzing Game Manual',
  },
};

/**
 * Without cancel button
 * Modal with cancel button hidden.
 */
export const WithoutCancelButton: Story = {
  render: args => <InteractiveWrapper scenario="embedding" {...args} />,
  args: {
    documentId: 'doc-123',
    hideCancelButton: true,
  },
};
