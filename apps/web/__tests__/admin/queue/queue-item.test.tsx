/**
 * QueueItem Component Tests (Issue #4737)
 *
 * Tests for individual queue item rendering:
 * - Status badge styling for all 5 statuses
 * - Conditional drag handle (Queued only)
 * - Priority display (Queued only)
 * - Retry info display (Failed with retryCount > 0)
 * - Current step display
 * - Selection highlighting
 * - Click-to-select behavior
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { DndContext } from '@dnd-kit/core';
import { SortableContext } from '@dnd-kit/sortable';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn(),
  },
}));

import { QueueItem } from '@/app/admin/(dashboard)/knowledge-base/queue/components/queue-item';
import type {
  ProcessingJobDto,
  JobStatus,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

function makeJob(overrides: Partial<ProcessingJobDto> = {}): ProcessingJobDto {
  return {
    id: 'job-1',
    pdfDocumentId: 'pdf-1',
    pdfFileName: 'test-document.pdf',
    userId: 'user-1',
    status: 'Queued' as JobStatus,
    priority: 5,
    currentStep: null,
    createdAt: new Date().toISOString(),
    startedAt: null,
    completedAt: null,
    errorMessage: null,
    retryCount: 0,
    maxRetries: 3,
    canRetry: false,
    ...overrides,
  };
}

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false } },
});

function renderQueueItem(job: ProcessingJobDto, isSelected = false, onSelect = vi.fn()) {
  return render(
    <QueryClientProvider client={queryClient}>
      <DndContext>
        <SortableContext items={[job.id]}>
          <QueueItem job={job} isSelected={isSelected} onSelect={onSelect} />
        </SortableContext>
      </DndContext>
    </QueryClientProvider>
  );
}

describe('QueueItem', () => {
  describe('File Name', () => {
    it('should display the PDF filename', () => {
      renderQueueItem(makeJob({ pdfFileName: 'my-rules.pdf' }));
      expect(screen.getByText('my-rules.pdf')).toBeInTheDocument();
    });
  });

  describe('Status Badges', () => {
    const statuses: JobStatus[] = ['Queued', 'Processing', 'Completed', 'Failed', 'Cancelled'];

    it.each(statuses)('should display badge for %s status', status => {
      renderQueueItem(makeJob({ status }));
      expect(screen.getByText(status)).toBeInTheDocument();
    });
  });

  describe('Drag Handle', () => {
    it('should show drag handle for Queued items', () => {
      renderQueueItem(makeJob({ status: 'Queued' }));
      expect(screen.getByLabelText(/Drag to reorder/)).toBeInTheDocument();
    });

    it('should include filename in drag handle aria-label', () => {
      renderQueueItem(makeJob({ status: 'Queued', pdfFileName: 'my-doc.pdf' }));
      expect(screen.getByLabelText('Drag to reorder my-doc.pdf')).toBeInTheDocument();
    });

    it('should not show drag handle for Processing items', () => {
      renderQueueItem(makeJob({ status: 'Processing' }));
      expect(screen.queryByLabelText(/Drag to reorder/)).not.toBeInTheDocument();
    });

    it('should not show drag handle for Completed items', () => {
      renderQueueItem(makeJob({ status: 'Completed' }));
      expect(screen.queryByLabelText(/Drag to reorder/)).not.toBeInTheDocument();
    });

    it('should not show drag handle for Failed items', () => {
      renderQueueItem(makeJob({ status: 'Failed' }));
      expect(screen.queryByLabelText(/Drag to reorder/)).not.toBeInTheDocument();
    });
  });

  describe('Priority', () => {
    it('should display priority for Queued items', () => {
      renderQueueItem(makeJob({ status: 'Queued', priority: 10 }));
      // PriorityBadge uses data-testid; priority 10 = 'normal' level
      expect(screen.getByTestId('priority-badge-normal')).toBeInTheDocument();
    });

    it('should not display priority for Processing items', () => {
      renderQueueItem(makeJob({ status: 'Processing', priority: 10 }));
      // PriorityBadge only renders for Queued status
      expect(screen.queryByTestId('priority-badge-normal')).not.toBeInTheDocument();
    });
  });

  describe('Retry Info', () => {
    it('should display retry count for Failed items with retries', () => {
      renderQueueItem(makeJob({ status: 'Failed', retryCount: 2, maxRetries: 3 }));
      expect(screen.getByText('Retry 2/3')).toBeInTheDocument();
    });

    it('should not display retry info for Failed items with zero retries', () => {
      renderQueueItem(makeJob({ status: 'Failed', retryCount: 0, maxRetries: 3 }));
      expect(screen.queryByText(/Retry/)).not.toBeInTheDocument();
    });

    it('should not display retry info for non-Failed items', () => {
      renderQueueItem(makeJob({ status: 'Processing', retryCount: 1, maxRetries: 3 }));
      expect(screen.queryByText(/Retry/)).not.toBeInTheDocument();
    });
  });

  describe('Current Step', () => {
    it('should display current step when present', () => {
      renderQueueItem(makeJob({ currentStep: 'Extracting Text' }));
      expect(screen.getByText('Extracting Text')).toBeInTheDocument();
    });

    it('should not display current step when null', () => {
      renderQueueItem(makeJob({ currentStep: null }));
      expect(screen.queryByText('Extracting Text')).not.toBeInTheDocument();
    });
  });

  describe('Selection', () => {
    it('should apply selection highlight when selected', () => {
      const { container } = renderQueueItem(makeJob(), true);
      const inner = container.querySelector('.bg-amber-50\\/80');
      expect(inner).toBeInTheDocument();
    });

    it('should call onSelect with jobId when clicked', () => {
      const onSelect = vi.fn();
      renderQueueItem(makeJob({ id: 'test-job-42', pdfFileName: 'click-me.pdf' }), false, onSelect);

      // Click the filename text - it's inside the selection button
      fireEvent.click(screen.getByText('click-me.pdf'));

      expect(onSelect).toHaveBeenCalledWith('test-job-42');
    });
  });

  describe('Relative Time', () => {
    it('should display "just now" for recent timestamps', () => {
      renderQueueItem(makeJob({ createdAt: new Date().toISOString() }));
      expect(screen.getByText('just now')).toBeInTheDocument();
    });

    it('should display minutes for recent timestamps', () => {
      const fiveMinAgo = new Date(Date.now() - 5 * 60_000).toISOString();
      renderQueueItem(makeJob({ createdAt: fiveMinAgo }));
      expect(screen.getByText('5m ago')).toBeInTheDocument();
    });

    it('should display hours for older timestamps', () => {
      const twoHoursAgo = new Date(Date.now() - 2 * 60 * 60_000).toISOString();
      renderQueueItem(makeJob({ createdAt: twoHoursAgo }));
      expect(screen.getByText('2h ago')).toBeInTheDocument();
    });

    it('should display days for very old timestamps', () => {
      const threeDaysAgo = new Date(Date.now() - 3 * 24 * 60 * 60_000).toISOString();
      renderQueueItem(makeJob({ createdAt: threeDaysAgo }));
      expect(screen.getByText('3d ago')).toBeInTheDocument();
    });
  });
});
