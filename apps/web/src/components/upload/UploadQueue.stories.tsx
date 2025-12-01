import type { Meta, StoryObj } from '@storybook/react';
import { UploadQueue } from './UploadQueue';
import type { UploadQueueItem, UploadQueueStats } from '@/hooks/useUploadQueue';

/**
 * UploadQueue - Display list of files being uploaded
 *
 * ## Features
 * - Aggregate progress header with overall stats
 * - Individual upload items with progress bars
 * - Status badges (pending, uploading, processing, succeeded, failed, cancelled)
 * - Clear completed button
 * - Empty state messaging
 * - Real-time progress tracking
 *
 * ## Accessibility
 * - ✅ Progress bars with ARIA labels
 * - ✅ Status badges with proper roles
 * - ✅ List semantics with role="list" and role="listitem"
 * - ✅ Action buttons with descriptive labels
 * - ✅ Live region updates for status changes
 */
const meta = {
  title: 'Upload/UploadQueue',
  component: UploadQueue,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Queue component displaying multiple file uploads with aggregate progress tracking, individual item status, and batch actions.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    items: {
      description: 'Array of upload queue items',
      control: 'object',
    },
    stats: {
      description: 'Aggregate upload statistics',
      control: 'object',
    },
  },
} satisfies Meta<typeof UploadQueue>;

export default meta;
type Story = StoryObj<typeof meta>;

// Fixed test data
const mockFile = (name: string, size: number) =>
  new File([new Uint8Array(size)], name, {
    type: 'application/pdf',
    lastModified: new Date('2024-01-15T10:00:00Z').getTime(),
  });

const mockItems: UploadQueueItem[] = [
  {
    id: '1',
    file: mockFile('gloomhaven-rules.pdf', 5242880),
    status: 'uploading',
    progress: 65,
    error: null,
    retryCount: 0,
    correlationId: null,
  },
  {
    id: '2',
    file: mockFile('wingspan-expansion.pdf', 3145728),
    status: 'pending',
    progress: 0,
    error: null,
    retryCount: 0,
    correlationId: null,
  },
  {
    id: '3',
    file: mockFile('terraforming-mars.pdf', 7340032),
    status: 'failed',
    progress: 45,
    error: 'Upload timeout after 30 seconds',
    retryCount: 2,
    correlationId: 'err-abc123',
  },
  {
    id: '4',
    file: mockFile('spirit-island-guide.pdf', 4194304),
    status: 'processing',
    progress: 100,
    error: null,
    retryCount: 0,
    correlationId: null,
  },
  {
    id: '5',
    file: mockFile('scythe-quickstart.pdf', 2097152),
    status: 'success',
    progress: 100,
    error: null,
    retryCount: 0,
    correlationId: null,
  },
];

const mixedStats: UploadQueueStats = {
  total: 5,
  pending: 1,
  uploading: 1,
  processing: 1,
  succeeded: 1,
  failed: 1,
  cancelled: 0,
};

/**
 * Empty queue state.
 * Shows message when no files are selected.
 */
export const Empty: Story = {
  args: {
    items: [],
    stats: {
      total: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
      succeeded: 0,
      failed: 0,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * Single file uploading.
 * Shows progress bar at 65% completion.
 */
export const SingleFile: Story = {
  args: {
    items: [mockItems[0]],
    stats: {
      total: 1,
      pending: 0,
      uploading: 1,
      processing: 0,
      succeeded: 0,
      failed: 0,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * Multiple files in various states.
 * Shows pending, uploading, processing, success, and failed states.
 */
export const MultipleFiles: Story = {
  args: {
    items: mockItems,
    stats: mixedStats,
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * All files completed successfully.
 * Shows all green status badges with clear button.
 */
export const AllCompleted: Story = {
  args: {
    items: mockItems.map(item => ({ ...item, status: 'success' as const, progress: 100 })),
    stats: {
      total: 5,
      pending: 0,
      uploading: 0,
      processing: 0,
      succeeded: 5,
      failed: 0,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * All files failed.
 * Shows all failed uploads requiring retry.
 */
export const AllFailed: Story = {
  args: {
    items: mockItems.map(item => ({
      ...item,
      status: 'failed' as const,
      error: 'Network error: ECONNREFUSED',
      correlationId: `err-${item.id}`,
    })),
    stats: {
      total: 5,
      pending: 0,
      uploading: 0,
      processing: 0,
      succeeded: 0,
      failed: 5,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * Some files with failures.
 * Mixed success/failure state requiring attention.
 */
export const WithFailures: Story = {
  args: {
    items: [
      mockItems[0],
      { ...mockItems[2], status: 'failed' as const },
      { ...mockItems[4], status: 'success' as const },
    ],
    stats: {
      total: 3,
      pending: 0,
      uploading: 1,
      processing: 0,
      succeeded: 1,
      failed: 1,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * Files with cancelled uploads.
 * Shows cancelled status badges.
 */
export const WithCancelled: Story = {
  args: {
    items: mockItems.map((item, idx) => ({
      ...item,
      status: idx % 2 === 0 ? ('cancelled' as const) : ('success' as const),
    })),
    stats: {
      total: 5,
      pending: 0,
      uploading: 0,
      processing: 0,
      succeeded: 2,
      failed: 0,
      cancelled: 3,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * All pending files.
 * Shows files waiting to start upload.
 */
export const AllPending: Story = {
  args: {
    items: mockItems.map(item => ({ ...item, status: 'pending' as const, progress: 0 })),
    stats: {
      total: 5,
      pending: 5,
      uploading: 0,
      processing: 0,
      succeeded: 0,
      failed: 0,
      cancelled: 0,
    },
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
};

/**
 * Dark theme variant.
 * Shows queue on dark background.
 */
export const DarkTheme: Story = {
  args: {
    items: mockItems,
    stats: mixedStats,
    onCancel: () => {},
    onRetry: () => {},
    onRemove: () => {},
    onClearCompleted: () => {},
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
