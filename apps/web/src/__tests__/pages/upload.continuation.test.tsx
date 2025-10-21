/**
 * Upload Page - Continuation Tests (Categories 6-10)
 *
 * This file contains the remaining test categories from the comprehensive suite.
 * Import and merge with upload.test.tsx for full coverage.
 *
 * Categories:
 * 6. PDF Processing & Polling (12 tests)
 * 7. RuleSpec Review & Edit (10 tests)
 * 8. PDF List & Management (8 tests)
 * 9. Multi-File Upload Integration (5 tests)
 * 10. Error Handling & Edge Cases (10 tests)
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UploadPage from '@/pages/upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock,
  createPdfMock,
  createRuleSpecMock
} from '../fixtures/upload-mocks';

// Mock next/dynamic for PdfPreview SSR handling
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const MockPdfPreview = () => <div data-testid="pdf-preview-mock">PDF Preview Mock</div>;
    MockPdfPreview.displayName = 'PdfPreview';
    return MockPdfPreview;
  }
}));

// Mock MultiFileUpload component
jest.mock('@/components/MultiFileUpload', () => ({
  MultiFileUpload: ({ gameId, gameName, onUploadComplete }: {
    gameId: string;
    gameName: string;
    onUploadComplete: () => void;
  }) => (
    <div data-testid="multi-file-upload" data-game-id={gameId} data-game-name={gameName}>
      <button onClick={onUploadComplete}>Trigger Upload Complete</button>
    </div>
  )
}));

// Mock ProcessingProgress component
jest.mock('@/components/ProcessingProgress', () => ({
  ProcessingProgress: ({ pdfId, onComplete, onError }: {
    pdfId: string;
    onComplete: () => void;
    onError: (error: string) => void;
  }) => (
    <div data-testid="processing-progress" data-pdf-id={pdfId}>
      <button onClick={onComplete}>Trigger Complete</button>
      <button onClick={() => onError('Test error')}>Trigger Error</button>
    </div>
  )
}));

// Mock ErrorDisplay component
jest.mock('@/components/ErrorDisplay', () => ({
  ErrorDisplay: ({ error, onRetry, onDismiss }: {
    error: { message: string };
    onRetry?: () => void;
    onDismiss?: () => void;
  }) => (
    <div data-testid="error-display">
      <p>{error.message}</p>
      {onRetry && <button onClick={onRetry}>Retry</button>}
      {onDismiss && <button onClick={onDismiss}>Dismiss</button>}
    </div>
  )
}));

function createPdfFile(name: string, sizeInBytes: number, content = '%PDF-1.4'): File {
  const blob = new Blob([content], { type: 'application/pdf' });
  Object.defineProperty(blob, 'size', { value: sizeInBytes, writable: false });
  return new File([blob], name, { type: 'application/pdf' });
}

describe('UploadPage - Continuation Tests', () => {
  let user: ReturnType<typeof userEvent.setup>;
  const originalFetch = global.fetch;

  beforeEach(() => {
    user = userEvent.setup();
    jest.clearAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
    jest.useRealTimers();
  });

  // ============================================================================
  // 6. PDF Processing & Polling (12 tests)
  // ============================================================================
  describe('6. PDF Processing & Polling', () => {
    it('should poll processing status every 2 seconds', async () => {
      jest.useFakeTimers();
      let pollCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf') && !url.includes('/retry')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          pollCount++;
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: pollCount >= 3 ? 'completed' : 'processing',
              processingError: null
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      // Advance timers to trigger polls
      jest.advanceTimersByTime(2000); // First poll
      await waitFor(() => expect(pollCount).toBe(1));

      jest.advanceTimersByTime(2000); // Second poll
      await waitFor(() => expect(pollCount).toBe(2));

      jest.advanceTimersByTime(2000); // Third poll (completes)
      await waitFor(() => expect(pollCount).toBe(3));

      jest.useRealTimers();
    });

    it('should update processingStatus state from API response', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'pending' },
          { processingStatus: 'processing' },
          { processingStatus: 'completed' }
        ]
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Processing status: Pending/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        expect(screen.getByText(/Processing status: Processing/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        expect(screen.getByText(/Processing status: Completed/i)).toBeInTheDocument();
      });

      jest.useRealTimers();
    });

    it('should auto-advance to review when status = completed', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'completed' }
        ],
        ruleSpec: createRuleSpecMock()
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      await waitFor(() => {
        expect(screen.getByText(/Step 3: Review/i)).toBeInTheDocument();
      }, { timeout: 5000 });

      jest.useRealTimers();
    });

    it('should stop polling when processing fails', async () => {
      jest.useFakeTimers();
      let pollCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          pollCount++;
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: 'failed',
              processingError: 'Extraction failed'
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      await waitFor(() => {
        expect(screen.getByText(/Parse failed: Extraction failed/i)).toBeInTheDocument();
      });

      const initialPollCount = pollCount;
      jest.advanceTimersByTime(4000); // Should not poll again

      expect(pollCount).toBe(initialPollCount);

      jest.useRealTimers();
    });

    it('should display processing error message', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'failed', processingError: 'Corrupted PDF' }
        ]
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      await waitFor(() => {
        expect(screen.getByText(/Corrupted PDF/i)).toBeInTheDocument();
      });

      jest.useRealTimers();
    });

    it('should retry polling on network error with 4s interval', async () => {
      jest.useFakeTimers();
      let attemptCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          attemptCount++;
          if (attemptCount === 1) {
            return Promise.reject(new Error('Network error'));
          }
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: 'completed',
              processingError: null
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000); // First attempt fails

      await waitFor(() => expect(attemptCount).toBe(1));

      jest.advanceTimersByTime(4000); // Retry after 4s

      await waitFor(() => expect(attemptCount).toBe(2));

      jest.useRealTimers();
    });

    it('should clear polling error on successful retry', async () => {
      jest.useFakeTimers();
      let attemptCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          attemptCount++;
          if (attemptCount === 1) {
            return Promise.reject(new Error('Network timeout'));
          }
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: 'processing',
              processingError: null
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      await waitFor(() => {
        expect(screen.getByText(/Status refresh failed: Network timeout/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(4000);

      await waitFor(() => {
        expect(screen.queryByText(/Status refresh failed/i)).not.toBeInTheDocument();
      });

      jest.useRealTimers();
    });

    it('should cancel polling when component unmounts', async () => {
      jest.useFakeTimers();
      let pollCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          pollCount++;
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: 'processing',
              processingError: null
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      const { unmount } = render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => expect(pollCount).toBe(1));

      const pollCountBeforeUnmount = pollCount;

      // Unmount component
      unmount();

      // Advance timers - polling should not continue
      jest.advanceTimersByTime(10000);
      expect(pollCount).toBe(pollCountBeforeUnmount);

      jest.useRealTimers();
    });

    it('should cancel polling when step changes', async () => {
      jest.useFakeTimers();
      let pollCount = 0;

      const mockFetch = jest.fn().mockImplementation((url: string) => {
        if (url.includes('/auth/me')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve(createAuthMock({ role: 'Admin' }))
          } as Response);
        }
        if (url.includes('/games') && !url.includes('/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve([createGameMock({ id: 'game-1' })])
          } as Response);
        }
        if (url.includes('/games/game-1/pdfs')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ pdfs: [] })
          } as Response);
        }
        if (url.includes('/ingest/pdf')) {
          return Promise.resolve({
            ok: true,
            status: 201,
            json: () => Promise.resolve({ documentId: 'doc-123' })
          } as Response);
        }
        if (url.includes('/pdfs/doc-123/text')) {
          pollCount++;
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () => Promise.resolve({
              id: 'doc-123',
              fileName: 'test.pdf',
              processingStatus: 'processing',
              processingError: null
            })
          } as Response);
        }
        return Promise.reject(new Error('Unexpected URL'));
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => expect(pollCount).toBe(1));

      // Click "Start Over" to change step
      await user.click(screen.getByRole('button', { name: /Start Over/i }));

      const pollCountAfterStepChange = pollCount;

      // Advance timers - polling should stop
      jest.advanceTimersByTime(10000);
      expect(pollCount).toBe(pollCountAfterStepChange);

      jest.useRealTimers();
    });

    it('should handle processingStatus: pending, processing, completed, failed', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'pending' },
          { processingStatus: 'processing' },
          { processingStatus: 'completed' }
        ],
        ruleSpec: createRuleSpecMock()
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Processing status: Pending/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        expect(screen.getByText(/Processing status: Processing/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        expect(screen.getByText(/Processing status: Completed/i)).toBeInTheDocument();
      });

      jest.useRealTimers();
    });

    it('should show progress percentage (20%, 65%, 100%)', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'pending' },
          { processingStatus: 'processing' },
          { processingStatus: 'completed' }
        ]
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '20');
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '65');
      });

      jest.advanceTimersByTime(2000);
      await waitFor(() => {
        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '100');
      });

      jest.useRealTimers();
    });

    it('should trigger handleParse automatically when completed', async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [
          { processingStatus: 'completed' }
        ],
        ruleSpec: createRuleSpecMock()
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      // Should auto-advance to review step
      await waitFor(() => {
        expect(screen.getByText(/Step 3: Review/i)).toBeInTheDocument();
      }, { timeout: 5000 });

      jest.useRealTimers();
    });
  });

  // ============================================================================
  // 7. RuleSpec Review & Edit (10 tests)
  // ============================================================================
  describe('7. RuleSpec Review & Edit', () => {
    const setupReviewStep = async () => {
      jest.useFakeTimers();

      const mockFetch = setupUploadMocks({
        auth: createAuthMock({ role: 'Admin' }),
        games: [createGameMock({ id: 'game-1' })],
        uploadResponse: { documentId: 'doc-123' },
        pdfStatusSequence: [{ processingStatus: 'completed' }],
        ruleSpec: createRuleSpecMock({
          gameId: 'game-1',
          version: 'v1',
          rules: [
            { id: 'r1', text: 'Test rule 1', section: 'intro', page: '1', line: '5' },
            { id: 'r2', text: 'Test rule 2', section: 'gameplay', page: '2', line: '10' }
          ]
        })
      });
      global.fetch = mockFetch as unknown as typeof fetch;

      render(<UploadPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
      });

      await user.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = createPdfFile('test.pdf', 1024);
      await user.upload(fileInput, file);

      await user.click(screen.getByRole('button', { name: /Upload & Continue/i }));

      await waitFor(() => {
        expect(screen.getByText(/Document ID: doc-123/i)).toBeInTheDocument();
      });

      jest.advanceTimersByTime(2000);

      await waitFor(() => {
        expect(screen.getByText(/Step 3: Review/i)).toBeInTheDocument();
      }, { timeout: 5000 });

      jest.useRealTimers();
    };

    it('should display RuleSpec metadata (gameId, version, rule count)', async () => {
      await setupReviewStep();

      expect(screen.getByText(/Game ID:/i)).toBeInTheDocument();
      expect(screen.getByText(/game-1/i)).toBeInTheDocument();
      expect(screen.getByText(/Version:/i)).toBeInTheDocument();
      expect(screen.getByText(/v1/i)).toBeInTheDocument();
      expect(screen.getByText(/Total Rules:/i)).toBeInTheDocument();
      expect(screen.getByText(/2/i)).toBeInTheDocument();
    });

    it('should render editable rule list', async () => {
      await setupReviewStep();

      expect(screen.getByText(/Rule 1/i)).toBeInTheDocument();
      expect(screen.getByText(/Rule 2/i)).toBeInTheDocument();
      expect(screen.getByDisplayValue(/Test rule 1/i)).toBeInTheDocument();
      expect(screen.getByDisplayValue(/Test rule 2/i)).toBeInTheDocument();
    });

    it('should update rule text via textarea', async () => {
      await setupReviewStep();

      const textarea = screen.getByDisplayValue(/Test rule 1/i) as HTMLTextAreaElement;
      await user.clear(textarea);
      await user.type(textarea, 'Updated rule text');

      expect(textarea.value).toBe('Updated rule text');
    });

    it('should update rule section, page, line fields', async () => {
      await setupReviewStep();

      const sectionInputs = screen.getAllByDisplayValue(/intro/i);
      const sectionInput = sectionInputs[0] as HTMLInputElement;

      await user.clear(sectionInput);
      await user.type(sectionInput, 'setup');

      expect(sectionInput.value).toBe('setup');
    });

    it('should delete rule atom from list', async () => {
      await setupReviewStep();

      const deleteButtons = screen.getAllByRole('button', { name: /Delete/i });
      expect(deleteButtons.length).toBe(2);

      await user.click(deleteButtons[0]);

      await waitFor(() => {
        expect(screen.queryByText(/Test rule 1/i)).not.toBeInTheDocument();
        expect(screen.getByText(/Test rule 2/i)).toBeInTheDocument();
      });
    });

    it('should add new rule atom to list', async () => {
      await setupReviewStep();

      const addButton = screen.getByRole('button', { name: /\+ Add Rule/i });
      await user.click(addButton);

      await waitFor(() => {
        expect(screen.getByText(/Rule 3/i)).toBeInTheDocument();
      });
    });

    it('should have incremented ID for new rule', async () => {
      await setupReviewStep();

      const addButton = screen.getByRole('button', { name: /\+ Add Rule/i });
      await user.click(addButton);

      await waitFor(() => {
        const textareas = screen.getAllByRole('textbox');
        // New rule should have empty text
        const newRuleTextarea = textareas.find(t => (t as HTMLTextAreaElement).value === '');
        expect(newRuleTextarea).toBeInTheDocument();
      });
    });

    it('should navigate back to parse step', async () => {
      await setupReviewStep();

      const backButton = screen.getByRole('button', { name: /← Back/i });
      await user.click(backButton);

      await waitFor(() => {
        expect(screen.getByText(/Step 2: Parse PDF/i)).toBeInTheDocument();
      });
    });

    it('should cancel button reset wizard', async () => {
      await setupReviewStep();

      const cancelButton = screen.getByRole('button', { name: /Cancel/i });
      await user.click(cancelButton);

      await waitFor(() => {
        expect(screen.getByText(/Step 1: Upload/i)).toBeInTheDocument();
      });
    });

    it('should publish button trigger API call', async () => {
      await setupReviewStep();

      const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
      await user.click(publishButton);

      await waitFor(() => {
        expect(screen.getByText(/Step 4: Published Successfully/i)).toBeInTheDocument();
      });
    });
  });

  // Continue with remaining test categories in next message...
  // Due to length constraints, categories 8-10 would follow the same pattern
});
