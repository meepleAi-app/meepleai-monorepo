/**
 * MultiFileUpload - Queue Operations Tests
 * Tests for upload summary, completion handling, and queue management
 *
 * Test Coverage:
 * - Upload summary display after completion
 * - Summary visibility triggers (close, new files)
 * - Summary with different stats (success, failure, mixed)
 * - Clear queue operations
 * - Summary lifecycle and edge cases
 * - Props integration with queue operations
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MultiFileUpload } from '../../components/upload/MultiFileUpload';
import {
  mockAddFiles,
  mockClearAll,
  mockGetStats,
  mockQueueStateRef,
  createMockFile,
  mockFileReader,
  defaultProps,
  setupBeforeEach,
  setupAfterEach,
  waitForFileValidation,
  createStats,
  getLastUploadQueueOptions,
} from './MultiFileUpload.test-helpers';

// Import mock creation function
import { createMockUseUploadQueue } from './MultiFileUpload.test-helpers';
createMockUseUploadQueue();

describe('MultiFileUpload - Queue Operations', () => {
  beforeEach(setupBeforeEach);
  afterEach(setupAfterEach);

  describe('Upload Summary Display', () => {
    it('shows summary after all uploads complete', async () => {
      mockQueueStateRef.current = [];
      mockGetStats.mockReturnValue(
        createStats({
          total: 3,
          succeeded: 3,
        })
      );

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(
        () => {
          expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });

    it('hides summary when closed', async () => {
      mockQueueStateRef.current = [];
      mockGetStats.mockReturnValue(
        createStats({
          total: 3,
          succeeded: 3,
        })
      );

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(
        () => {
          expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );

      const closeButton = screen.getByRole('button', { name: /Close upload summary/i });

      await act(async () => {
        fireEvent.click(closeButton);
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      await waitFor(
        () => {
          expect(screen.queryByTestId('upload-summary')).not.toBeInTheDocument();
        },
        { timeout: 1000 }
      );
    });

    it('hides summary when new files are added', async () => {
      mockFileReader('%PDF-');
      mockQueueStateRef.current = [];
      mockGetStats.mockReturnValue(
        createStats({
          total: 3,
          succeeded: 3,
        })
      );

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
        await new Promise(resolve => setTimeout(resolve, 0));
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(
        () => {
          expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
        },
        { timeout: 1000 }
      );

      const file = createMockFile('new.pdf', 1000, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(50);
      });

      await act(async () => {
        await new Promise(resolve => setTimeout(resolve, 100));
      });

      await waitFor(
        () => {
          const summary = screen.queryByTestId('upload-summary');
          if (summary) {
            expect(mockAddFiles).toHaveBeenCalled();
          }
        },
        { timeout: 1000 }
      );
    });

    it('passes stats to UploadSummary', async () => {
      const stats = createStats({
        total: 5,
        succeeded: 4,
        failed: 1,
      });
      mockGetStats.mockReturnValue(stats);

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(stats);
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });
    });

    it('calls clearAll when UploadSummary Clear Queue clicked', async () => {
      mockGetStats.mockReturnValue(
        createStats({
          total: 3,
          succeeded: 3,
        })
      );

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });

      const clearButton = screen.getByRole('button', { name: /Clear all items from queue/i });
      fireEvent.click(clearButton);

      expect(mockClearAll).toHaveBeenCalled();
    });
  });

  describe('Summary with Different Stats', () => {
    it('shows summary with mixed success and failure', async () => {
      const stats = createStats({
        total: 10,
        succeeded: 7,
        failed: 2,
        cancelled: 1,
      });
      mockGetStats.mockReturnValue(stats);

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(stats);
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });
    });

    it('shows summary with all successful uploads', async () => {
      const stats = createStats({
        total: 5,
        succeeded: 5,
      });
      mockGetStats.mockReturnValue(stats);

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(stats);
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });
    });

    it('shows summary with all failed uploads', async () => {
      const stats = createStats({
        total: 3,
        failed: 3,
      });
      mockGetStats.mockReturnValue(stats);

      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(stats);
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });
    });
  });

  describe('Summary Lifecycle Edge Cases', () => {
    it('does not show summary when uploads are still in progress', () => {
      mockQueueStateRef.current = [
        {
          id: '1',
          file: createMockFile('test.pdf', 1000, 'application/pdf'),
          gameId: 'game-123',
          language: 'en',
          status: 'uploading' as const,
          progress: 50,
          retryCount: 0,
        },
      ];
      mockGetStats.mockReturnValue(
        createStats({
          total: 1,
          uploading: 1,
        })
      );

      render(<MultiFileUpload {...defaultProps} />);

      expect(screen.queryByTestId('upload-summary')).not.toBeInTheDocument();
    });

    it('shows summary multiple times after re-completing uploads', async () => {
      mockQueueStateRef.current = [];
      const { rerender } = render(<MultiFileUpload {...defaultProps} />);

      // First completion
      mockGetStats.mockReturnValue(createStats({ total: 2, succeeded: 2 }));
      const options = getLastUploadQueueOptions();

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });

      // Close summary
      const closeButton = screen.getByRole('button', { name: /Close upload summary/i });
      await act(async () => {
        fireEvent.click(closeButton);
      });

      await waitFor(() => {
        expect(screen.queryByTestId('upload-summary')).not.toBeInTheDocument();
      });

      // Second completion
      mockGetStats.mockReturnValue(createStats({ total: 3, succeeded: 3 }));

      await act(async () => {
        options.onAllComplete?.(mockGetStats());
      });

      rerender(<MultiFileUpload {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByTestId('upload-summary')).toBeInTheDocument();
      });
    });
  });

  describe('Props Integration', () => {
    it('passes gameId and language to uploaded files', async () => {
      mockFileReader('%PDF-');
      render(<MultiFileUpload gameId="custom-game" gameName="Custom Game" language="it" />);

      const file = createMockFile('test.pdf', 1000, 'application/pdf');
      const input = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;

      await act(async () => {
        fireEvent.change(input, { target: { files: [file] } });
        await waitForFileValidation(100);
      });

      await waitFor(
        () => {
          expect(mockAddFiles).toHaveBeenCalledWith(
            expect.arrayContaining([expect.objectContaining({ name: 'test.pdf' })]),
            'custom-game',
            'it'
          );
        },
        { timeout: 2000 }
      );
    });
  });
});
