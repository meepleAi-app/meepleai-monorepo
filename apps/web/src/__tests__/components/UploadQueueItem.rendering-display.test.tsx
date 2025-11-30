/**
 * UploadQueueItem Component Tests - Rendering and Display
 * Tests cover status states, file information, progress bars, and error display
 */

import { render, screen } from '@testing-library/react';
import { UploadQueueItem } from '@/components/upload/UploadQueueItem';
import {
  createTestItem,
  createFileWithSize,
  createMockCallbacks,
  UPLOAD_STATUS,
  FILE_SIZES,
  FILE_SIZE_DISPLAY,
  ERROR_MESSAGES
} from './UploadQueueItem.test-helpers';

describe('UploadQueueItem Component - Rendering and Display', () => {
  let mockCallbacks: ReturnType<typeof createMockCallbacks>;

  beforeEach(() => {
    mockCallbacks = createMockCallbacks();
  });

  describe('Rendering - Status States', () => {
    it('renders pending status correctly', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.PENDING });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Pending')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Remove test.pdf from queue/i })).toBeInTheDocument();
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it('renders uploading status with progress bar', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.UPLOADING, progress: 45 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Uploading')).toBeInTheDocument();
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      expect(screen.getByText('45% complete')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Cancel upload of test.pdf/i })).toBeInTheDocument();
    });

    it('renders processing status with progress bar', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.PROCESSING, progress: 75 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Processing')).toBeInTheDocument();
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      expect(screen.getByText('75% complete')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Cancel upload of test.pdf/i })).toBeInTheDocument();
    });

    it('renders success status', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.SUCCESS, progress: 100 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Success')).toBeInTheDocument();
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('renders failed status with error message', () => {
      const item = createTestItem({
        status: UPLOAD_STATUS.FAILED,
        error: ERROR_MESSAGES.UPLOAD_FAILED
      });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Failed')).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent(`Error: ${ERROR_MESSAGES.UPLOAD_FAILED}`);
      expect(screen.getByRole('button', { name: /Retry upload of test.pdf/i })).toBeInTheDocument();
    });

    it('renders cancelled status', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.CANCELLED });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('Cancelled')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Remove test.pdf from queue/i })).toBeInTheDocument();
    });
  });

  describe('File Information', () => {
    it('displays filename with title attribute', () => {
      const item = createTestItem({
        file: new File(['content'], 'very-long-filename-for-testing.pdf', { type: 'application/pdf' })
      });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const filename = screen.getByText('very-long-filename-for-testing.pdf');
      expect(filename).toHaveAttribute('title', 'very-long-filename-for-testing.pdf');
    });

    it('formats file size in bytes', () => {
      const file = createFileWithSize('test.pdf', FILE_SIZES.BYTES);
      const item = createTestItem({ file });

      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText(FILE_SIZE_DISPLAY.BYTES)).toBeInTheDocument();
    });

    it('formats file size in KB', () => {
      const file = createFileWithSize('test.pdf', FILE_SIZES.KILOBYTES);
      const item = createTestItem({ file });

      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText(FILE_SIZE_DISPLAY.KILOBYTES)).toBeInTheDocument();
    });

    it('formats file size in MB', () => {
      const file = createFileWithSize('test.pdf', FILE_SIZES.MEGABYTES);
      const item = createTestItem({ file });

      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText(FILE_SIZE_DISPLAY.MEGABYTES)).toBeInTheDocument();
    });

    it('displays retry count when greater than 0', () => {
      const item = createTestItem({ retryCount: 2 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText(/Retry 2/i)).toBeInTheDocument();
    });

    it('does not display retry count when 0', () => {
      const item = createTestItem({ retryCount: 0 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.queryByText(/Retry/i)).not.toBeInTheDocument();
    });
  });

  describe('Progress Bar', () => {
    it('displays progress bar for uploading status', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.UPLOADING, progress: 30 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Upload progress for test.pdf/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '30');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
    });

    it('displays progress bar for processing status', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.PROCESSING, progress: 60 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Upload progress for test.pdf/i });
      expect(progressBar).toBeInTheDocument();
      expect(screen.getByText('60% complete')).toBeInTheDocument();
    });

    it('hides progress bar for non-active states', () => {
      const statuses: Array<'pending' | 'success' | 'failed' | 'cancelled'> = [
        UPLOAD_STATUS.PENDING,
        UPLOAD_STATUS.SUCCESS,
        UPLOAD_STATUS.FAILED,
        UPLOAD_STATUS.CANCELLED
      ];

      statuses.forEach((status) => {
        const { unmount } = render(
          <UploadQueueItem
            item={createTestItem({ status })}
            {...mockCallbacks}
          />
        );

        expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
        unmount();
      });
    });
  });

  describe('Error Display', () => {
    it('displays error message for failed uploads', () => {
      const item = createTestItem({
        status: UPLOAD_STATUS.FAILED,
        error: ERROR_MESSAGES.SERVER
      });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const errorAlert = screen.getByRole('alert');
      expect(errorAlert).toHaveTextContent(`Error: ${ERROR_MESSAGES.SERVER}`);
    });

    it('displays correlation ID when present', () => {
      const item = createTestItem({
        status: UPLOAD_STATUS.FAILED,
        error: ERROR_MESSAGES.UPLOAD_FAILED,
        correlationId: 'corr-123-456'
      });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const errorAlert = screen.getByRole('alert');
      expect(errorAlert).toHaveTextContent('Error ID: corr-123-456');
    });

    it('does not display error for non-failed statuses', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.SUCCESS });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles very long filenames with ellipsis', () => {
      const longFilename = 'a'.repeat(200) + '.pdf';
      const file = new File(['content'], longFilename, { type: 'application/pdf' });
      const item = createTestItem({ file });

      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const filenameElement = screen.getByTitle(longFilename);
      expect(filenameElement).toBeInTheDocument();
    });

    it('handles error without correlation ID', () => {
      const item = createTestItem({
        status: UPLOAD_STATUS.FAILED,
        error: 'Simple error',
        correlationId: undefined
      });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      const errorAlert = screen.getByRole('alert');
      expect(errorAlert).toHaveTextContent('Error: Simple error');
      expect(errorAlert).not.toHaveTextContent('Error ID');
    });

    it('handles zero file size', () => {
      const file = createFileWithSize('empty.pdf', 0);
      const item = createTestItem({ file });

      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText(FILE_SIZE_DISPLAY.ZERO)).toBeInTheDocument();
    });

    it('handles 100% progress', () => {
      const item = createTestItem({ status: UPLOAD_STATUS.PROCESSING, progress: 100 });
      render(
        <UploadQueueItem
          item={item}
          {...mockCallbacks}
        />
      );

      expect(screen.getByText('100% complete')).toBeInTheDocument();
    });
  });
});
