/**
 * Upload Page - PDF Upload Workflow Tests
 *
 * BDD Scenarios:
 * - PDF upload and status polling
 * - Retry failed PDF parsing
 * - Error handling for upload failures
 * - PDF list display and file operations
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock,
  createPdfMock,
  createRuleSpecMock
} from '../../__tests__/fixtures/upload-mocks';

describe('UploadPage - PDF Upload', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('Given user uploads PDF successfully', () => {
    describe('When PDF processing completes', () => {
      it('Then auto advances to review step', async () => {
        jest.useFakeTimers();

        try {
          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-3', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [] },
            uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
            pdfStatusSequence: [
              { processingStatus: 'processing' },
              { processingStatus: 'completed' }
            ],
            ruleSpec: createRuleSpecMock({
              gameId: 'game-1',
              rules: [{ id: 'r1', text: 'Auto generated rule', section: 'Intro', page: '1', line: '1' }]
            })
          });

          global.fetch = mockFetch as unknown as typeof fetch;

          render(<UploadPage />);

          await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

          fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
          const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

          fireEvent.change(fileInput, { target: { files: [file] } });
          await waitFor(() => expect(uploadButton).not.toBeDisabled());

          fireEvent.click(uploadButton);

          await waitFor(() => expect(screen.getByText(/Processing status/i)).toBeInTheDocument());
          expect(screen.getByText(/Processing status/i)).toHaveTextContent(/Processing status: (Pending|Processing)/i);

          const continueButton = screen.getByRole('button', { name: /Parse PDF/i });
          expect(continueButton).toBeDisabled();

          await waitFor(() =>
            expect(screen.getByText(/Processing status/i)).toHaveTextContent('Processing')
          );

          await act(async () => {
            jest.advanceTimersByTime(2000);
          });

          await waitFor(() =>
            expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument()
          );

          await waitFor(() =>
            expect(mockFetch).toHaveBeenCalledWith(
              expect.stringContaining('/games/game-1/rulespec'),
              expect.objectContaining({ method: 'GET' })
            )
          );

          await waitFor(() => expect(screen.getByText(/Auto generated rule/i)).toBeInTheDocument());
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When PDF status polling encounters network error', () => {
      it('Then shows error but continues polling until success', async () => {
        jest.useFakeTimers();

        try {
          let statusCallCount = 0;

          const mockFetch = jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
            const url = typeof input === 'string' ? input : input.toString();
            const method = init?.method ?? 'GET';

            if (url.endsWith('/auth/me')) {
              return setupUploadMocks({ auth: createAuthMock({ userId: 'user-6', role: 'Admin' }) })(url, init);
            }

            if (url.endsWith('/games') && method === 'GET') {
              return setupUploadMocks({ games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })] })(url, init);
            }

            if (url.includes('/games/game-1/pdfs')) {
              return setupUploadMocks({ pdfs: { pdfs: [] } })(url, init);
            }

            if (url.endsWith('/ingest/pdf')) {
              return setupUploadMocks({ uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' } })(url, init);
            }

            if (url.endsWith('/pdfs/pdf-123/text')) {
              statusCallCount += 1;
              if (statusCallCount === 1) {
                return Promise.reject(new Error('Network down'));
              }
              if (statusCallCount === 2) {
                return setupUploadMocks({
                  uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
                  pdfStatusSequence: [{ processingStatus: 'processing', processingError: null }]
                })(url, init);
              }
              return setupUploadMocks({
                uploadResponse: { documentId: 'pdf-123', fileName: 'rules.pdf' },
                pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }]
              })(url, init);
            }

            if (url.endsWith('/games/game-1/rulespec')) {
              return setupUploadMocks({ ruleSpec: createRuleSpecMock({ gameId: 'game-1', rules: [{ id: 'r1', text: 'Rule text' }] }) })(url, init);
            }

            throw new Error(`Unexpected fetch call to ${url}`);
          }) as jest.MockedFunction<typeof fetch>;

          global.fetch = mockFetch;

          render(<UploadPage />);

          await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

          fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
          fireEvent.change(fileInput, { target: { files: [file] } });

          const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
          await waitFor(() => expect(uploadButton).not.toBeDisabled());
          fireEvent.click(uploadButton);

          await waitFor(() =>
            expect(screen.getByText(/Status refresh failed: Network down/i)).toBeInTheDocument()
          );

          await act(async () => {
            jest.advanceTimersByTime(4000);
          });
          await act(async () => {
            jest.advanceTimersByTime(2000);
          });

          await waitFor(() =>
            expect(
              screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })
            ).toBeInTheDocument()
          );
        } finally {
          jest.useRealTimers();
        }
      });
    });
  });

  describe('Given user attempts to upload PDF', () => {
    describe('When upload request fails', () => {
      it('Then error message is displayed', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-7', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] },
          uploadError: { status: 500, error: 'Upload exploded' }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
        await waitFor(() => expect(uploadButton).not.toBeDisabled());
        fireEvent.click(uploadButton);

        await waitFor(() =>
          expect(screen.getByText(/❌ Upload failed: Upload exploded/i)).toBeInTheDocument()
        );
      });
    });

    describe('When upload request throws exception', () => {
      it('Then error message with exception details is displayed', async () => {
        const mockFetch = jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
          const url = typeof input === 'string' ? input : input.toString();
          const method = init?.method ?? 'GET';

          if (url.endsWith('/auth/me')) {
            return setupUploadMocks({ auth: createAuthMock({ userId: 'user-20', role: 'Admin' }) })(url, init);
          }

          if (url.endsWith('/games') && method === 'GET') {
            return setupUploadMocks({ games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })] })(url, init);
          }

          if (url.includes('/games/game-1/pdfs')) {
            return setupUploadMocks({ pdfs: { pdfs: [] } })(url, init);
          }

          if (url.endsWith('/ingest/pdf')) {
            return Promise.reject(new Error('Connection timeout'));
          }

          throw new Error(`Unexpected fetch call to ${url}`);
        }) as jest.MockedFunction<typeof fetch>;

        global.fetch = mockFetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
        await waitFor(() => expect(uploadButton).not.toBeDisabled());
        fireEvent.click(uploadButton);

        await waitFor(() =>
          expect(screen.getByText(/❌ Upload failed: Connection timeout/i)).toBeInTheDocument()
        );
      });
    });
  });

  describe('Given game has uploaded PDFs', () => {
    describe('When loading PDFs fails', () => {
      it('Then error message is displayed', async () => {
        const mockFetch = jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
          const url = typeof input === 'string' ? input : input.toString();
          const method = init?.method ?? 'GET';

          if (url.endsWith('/auth/me')) {
            return setupUploadMocks({ auth: createAuthMock({ userId: 'user-4', role: 'Admin' }) })(url, init);
          }

          if (url.endsWith('/games') && method === 'GET') {
            return setupUploadMocks({ games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })] })(url, init);
          }

          if (url.includes('/games/game-1/pdfs')) {
            return Promise.resolve({
              ok: false,
              status: 500,
              statusText: 'Internal Server Error',
              json: () => Promise.resolve({ error: 'Server error' })
            } as Response);
          }

          throw new Error(`Unexpected fetch call to ${url}`);
        }) as jest.MockedFunction<typeof fetch>;

        global.fetch = mockFetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        await waitFor(() =>
          expect(screen.getByText(/Unable to load uploaded PDFs\. Please try again\./i)).toBeInTheDocument()
        );
      });
    });

    describe('When loading PDFs throws exception', () => {
      it('Then error message is displayed', async () => {
        const mockFetch = jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
          const url = typeof input === 'string' ? input : input.toString();
          const method = init?.method ?? 'GET';

          if (url.endsWith('/auth/me')) {
            return setupUploadMocks({ auth: createAuthMock({ userId: 'user-19', role: 'Admin' }) })(url, init);
          }

          if (url.endsWith('/games') && method === 'GET') {
            return setupUploadMocks({ games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })] })(url, init);
          }

          if (url.includes('/games/game-1/pdfs')) {
            return Promise.reject(new Error('Network failure'));
          }

          throw new Error(`Unexpected fetch call to ${url}`);
        }) as jest.MockedFunction<typeof fetch>;

        global.fetch = mockFetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        await waitFor(() =>
          expect(screen.getByText(/Unable to load uploaded PDFs\. Please try again\./i)).toBeInTheDocument()
        );
      });
    });

    describe('When PDFs are displayed with various file sizes', () => {
      it('Then file sizes are formatted correctly', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Test' })],
          pdfs: {
            pdfs: [
              createPdfMock({
                id: 'pdf-1',
                fileName: 'tiny.pdf',
                fileSizeBytes: 512
              }),
              createPdfMock({
                id: 'pdf-2',
                fileName: 'small.pdf',
                fileSizeBytes: 2048
              })
            ]
          }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        await waitFor(() => {
          expect(screen.getByText('512 B')).toBeInTheDocument();
          expect(screen.getByText('2.0 KB')).toBeInTheDocument();
        });
      });
    });

    describe('When PDF has log URL', () => {
      it('Then log can be opened in new window', async () => {
        const windowOpenSpy = jest.spyOn(window, 'open').mockImplementation(() => null);

        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Test' })],
          pdfs: {
            pdfs: [
              createPdfMock({
                id: 'pdf-1',
                fileName: 'test.pdf',
                fileSizeBytes: 1024,
                logUrl: 'http://localhost:8080/logs/pdf-1'
              })
            ]
          }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        await waitFor(() => {
          expect(screen.getByText('test.pdf')).toBeInTheDocument();
        });

        const openLogButton = screen.getByRole('button', { name: /Open log/i });
        fireEvent.click(openLogButton);

        expect(windowOpenSpy).toHaveBeenCalledWith('http://localhost:8080/logs/pdf-1', '_blank', 'noopener,noreferrer');

        windowOpenSpy.mockRestore();
      });
    });

    describe('When PDF failed and retry is requested', () => {
      it('Then retry endpoint is called and success message shown', async () => {
        jest.useFakeTimers();

        try {
          const failedPdf = createPdfMock({
            id: 'pdf-failed',
            fileName: 'failed.pdf',
            fileSizeBytes: 1024,
            status: 'failed',
            logUrl: null
          });

          const mockFetch = setupUploadMocks({
            auth: createAuthMock({ userId: 'user-17', role: 'Admin' }),
            games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
            pdfs: { pdfs: [failedPdf] },
            retryParseResponse: { success: true }
          });

          global.fetch = mockFetch as unknown as typeof fetch;

          render(<UploadPage />);

          await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

          fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

          await waitFor(() => expect(screen.getByText(/failed.pdf/i)).toBeInTheDocument());

          const retryButton = screen.getByRole('button', { name: /Retry parsing/i });
          fireEvent.click(retryButton);

          await waitFor(() =>
            expect(screen.getByText(/✅ Parse re-triggered for failed.pdf/i)).toBeInTheDocument()
          );
        } finally {
          jest.useRealTimers();
        }
      });
    });

    describe('When retry parsing fails', () => {
      it('Then error message is displayed', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Test' })],
          pdfs: {
            pdfs: [
              createPdfMock({
                id: 'pdf-1',
                fileName: 'test.pdf',
                fileSizeBytes: 1024
              })
            ]
          },
          retryParseError: { status: 500, error: 'Retry failed' }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        await waitFor(() => {
          expect(screen.getByText('test.pdf')).toBeInTheDocument();
        });

        const retryButton = screen.getByRole('button', { name: /Retry parsing/i });
        fireEvent.click(retryButton);

        await waitFor(() => {
          expect(screen.getByText(/Failed to re-trigger parse: Retry failed/i)).toBeInTheDocument();
        });
      });
    });
  });

  describe('Given PDF status polling', () => {
    describe('When JSON parsing fails in polling', () => {
      it('Then error is shown but polling continues', async () => {
        jest.useFakeTimers();
        try {
          let pollCount = 0;

          const mockFetch = jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
            const url = typeof input === 'string' ? input : input.toString();
            const method = init?.method ?? 'GET';

            if (url.endsWith('/auth/me')) {
              return setupUploadMocks({ auth: createAuthMock({ userId: 'user-1', role: 'Admin' }) })(url, init);
            }

            if (url.endsWith('/games') && method === 'GET') {
              return setupUploadMocks({ games: [createGameMock({ id: 'game-1', name: 'Test Game' })] })(url, init);
            }

            if (url.includes('/games/game-1/pdfs')) {
              return setupUploadMocks({ pdfs: { pdfs: [] } })(url, init);
            }

            if (url.endsWith('/ingest/pdf')) {
              return setupUploadMocks({ uploadResponse: { documentId: 'pdf-123' } })(url, init);
            }

            if (url.endsWith('/pdfs/pdf-123/text')) {
              pollCount++;
              if (pollCount === 1) {
                return Promise.resolve({
                  ok: false,
                  status: 500,
                  statusText: 'Internal Server Error',
                  json: () => Promise.resolve(null)
                } as Response);
              }
              return setupUploadMocks({
                uploadResponse: { documentId: 'pdf-123' },
                pdfStatusSequence: [{ processingStatus: 'completed', processingError: null }]
              })(url, init);
            }

            if (url.endsWith('/games/game-1/rulespec')) {
              return setupUploadMocks({
                ruleSpec: createRuleSpecMock({ gameId: 'game-1', version: '1.0', rules: [] })
              })(url, init);
            }

            throw new Error(`Unexpected fetch call to ${url}`);
          }) as jest.MockedFunction<typeof fetch>;

          global.fetch = mockFetch;

          render(<UploadPage />);

          await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

          fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const file = new File(['pdf'], 'test.pdf', { type: 'application/pdf' });
          fireEvent.change(fileInput, { target: { files: [file] } });

          fireEvent.click(screen.getByRole('button', { name: /Upload & Continue/i }));

          await act(async () => {
            jest.advanceTimersByTime(5000);
          });

          await waitFor(() => {
            expect(screen.getByText(/Status refresh failed/i)).toBeInTheDocument();
          });
        } finally {
          jest.useRealTimers();
        }
      });
    });
  });

  describe('Given game ID is not confirmed', () => {
    describe('When PDFs would be loaded', () => {
      it('Then skips loading PDFs', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: []
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => {
          expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument();
        });

        // No PDFs should be loaded when no game is confirmed
        expect(mockFetch).not.toHaveBeenCalledWith(
          expect.stringContaining('/pdfs'),
          expect.anything()
        );
      });
    });
  });
});
