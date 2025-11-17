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
import UploadPage from '../../pages/upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock,
  createPdfMock,
  createRuleSpecMock
} from '../../pages/../__tests__/fixtures/upload-mocks';
import userEvent from '@testing-library/user-event';
import { setupWorkerMock, MockBroadcastChannel } from '../helpers/uploadQueueMocks';

describe('UploadPage - PDF Upload', () => {
  const originalFetch = global.fetch;
  const originalWorker = global.Worker;
  const originalBroadcastChannel = global.BroadcastChannel;

  beforeEach(() => {
    jest.clearAllMocks();

    // Setup worker mock for all tests
    setupWorkerMock({ uploadDelay: 0, autoUpload: true });

    // Mock BroadcastChannel
    // @ts-expect-error - Mocking global BroadcastChannel for tests
    global.BroadcastChannel = MockBroadcastChannel;

    // Mock localStorage
    const localStorageMock: Storage = {
      getItem: jest.fn(),
      setItem: jest.fn(),
      removeItem: jest.fn(),
      clear: jest.fn(),
      key: jest.fn(),
      length: 0
    };
    Object.defineProperty(window, 'localStorage', { value: localStorageMock, writable: true });
  });

  afterEach(() => {
    // Restore globals after each test to prevent pollution
    global.fetch = originalFetch;
    global.Worker = originalWorker;
    global.BroadcastChannel = originalBroadcastChannel;
    jest.useRealTimers();
  });

  // Helper to setup game selection for tests
  async function confirmGameSelection(user: ReturnType<typeof userEvent.setup>) {
    // Wait for the Shadcn Select trigger button to be available
    const selectTrigger = await waitFor(() => {
      const trigger = screen.getByRole('combobox', { name: /select.*game/i });
      expect(trigger).toBeInTheDocument();
      return trigger;
    });

    // Open the select dropdown
    await user.click(selectTrigger);

    // Wait for and click the first game option
    const gameOptions = await waitFor(() => {
      const options = screen.getAllByRole('option');
      expect(options.length).toBeGreaterThan(0);
      return options;
    });
    
    await user.click(gameOptions[0]);

    // Now confirm selection
    const confirmButton = await waitFor(() => {
      const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
      expect(btn).not.toBeDisabled();
      return btn;
    });
    await user.click(confirmButton);
  }

  describe('Given user uploads PDF successfully', () => {
    describe('When PDF processing completes', () => {
      it('Then auto advances to review step', async () => {
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

          // Wait for Shadcn Select trigger button
          const selectTrigger = await waitFor(() => {
            const trigger = screen.getByRole('combobox', { name: /select.*game/i });
            expect(trigger).toBeInTheDocument();
            return trigger;
          });

          // Open the select dropdown
          fireEvent.click(selectTrigger);

          // Wait for and click the game option by role
          const gameOption = await waitFor(() => {
            // Use getAllByText since it might appear multiple times
            const options = screen.getAllByText('Terraforming Mars');
            // Find the one that's clickable (not in the trigger)
            return options.find(opt => opt.closest('[role="option"]')) || options[options.length - 1];
          });
          fireEvent.click(gameOption);

          // Now wait for and click the confirm button
          const confirmButton = await waitFor(() => {
            const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
            expect(btn).not.toBeDisabled();
            return btn;
          });
          fireEvent.click(confirmButton);

          // Wait for PDF upload form to appear
          await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

          // First, verify the button exists (might be disabled)
          const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });

          // Now change the file which should enable the button
          fireEvent.change(fileInput, { target: { files: [file] } });

          // Wait for button to become enabled
          await waitFor(() => expect(uploadButton).not.toBeDisabled());

          fireEvent.click(uploadButton);

          await waitFor(() => expect(screen.getByText(/Processing status/i)).toBeInTheDocument());
          expect(screen.getByText(/Processing status/i)).toHaveTextContent(/Processing status: (Pending|Processing)/i);

          const continueButton = screen.getByRole('button', { name: /Parse PDF/i });
          expect(continueButton).toBeDisabled();

          await waitFor(() =>
            expect(screen.getByText(/Processing status/i)).toHaveTextContent('Processing')
          );

          await waitFor(() =>
            expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument(),
            { timeout: 5000 }
          );

          await waitFor(() =>
            expect(mockFetch).toHaveBeenCalledWith(
              expect.stringContaining('/games/game-1/rulespec'),
              expect.objectContaining({ method: 'GET' })
            )
          );

          await waitFor(() => expect(screen.getByText(/Auto generated rule/i)).toBeInTheDocument());
      });
    });

    describe('When PDF status polling encounters network error', () => {
      it('Then shows error but continues polling until success', async () => {
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

          // Wait for Shadcn Select trigger button
          const selectTrigger = await waitFor(() => {
            const trigger = screen.getByRole('combobox', { name: /select.*game/i });
            expect(trigger).toBeInTheDocument();
            return trigger;
          });

          // Open the select dropdown
          fireEvent.click(selectTrigger);

          // Wait for and click the game option by role
          const gameOption = await waitFor(() => {
            // Use getAllByText since it might appear multiple times
            const options = screen.getAllByText('Terraforming Mars');
            // Find the one that's clickable (not in the trigger)
            return options.find(opt => opt.closest('[role="option"]')) || options[options.length - 1];
          });
          fireEvent.click(gameOption);

          // Now wait for and click the confirm button
          const confirmButton = await waitFor(() => {
            const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
            expect(btn).not.toBeDisabled();
            return btn;
          });
          fireEvent.click(confirmButton);

          // Wait for PDF upload form to appear
          await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());

          const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
          const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

          // First, verify the button exists (might be disabled)
          const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });

          // Now change the file which should enable the button
          fireEvent.change(fileInput, { target: { files: [file] } });

          // Wait for button to become enabled
          await waitFor(() => expect(uploadButton).not.toBeDisabled());
          fireEvent.click(uploadButton);

          await waitFor(() =>
            expect(screen.getByText(/Status refresh failed: Network down/i)).toBeInTheDocument()
          );

          await waitFor(() =>
            expect(
              screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })
            ).toBeInTheDocument(),
            { timeout: 10000 }
          );
      });
    });
  });

  describe('Given user attempts to upload PDF', () => {
    describe('When upload request fails', () => {
      it('Then error message is displayed', async () => {
        // Session 9: Using observability hooks for reliable error state tracking
        const uploadEvents: string[] = [];
        const UploadPageWithHooks = () => (
          <UploadPage
            autoUpload={false}
            onUploadStart={() => uploadEvents.push('upload_start')}
            onUploadError={() => uploadEvents.push('upload_error')}
          />
        );

        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-7', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] },
          uploadError: { status: 500, error: 'Upload exploded' }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPageWithHooks />);

        // Wait for Shadcn Select trigger button
        const selectTrigger = await waitFor(() => {
          const trigger = screen.getByRole('combobox', { name: /select.*game/i });
          expect(trigger).toBeInTheDocument();
          return trigger;
        });

        // Open the select dropdown
        fireEvent.click(selectTrigger);

        // Wait for and click the game option by text
        const gameOption = await waitFor(() => {
          const option = screen.getByText('Terraforming Mars');
          return option;
        });
        fireEvent.click(gameOption);

        // Now confirm selection
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

        // Wait for MultiFileUpload to appear
        await waitFor(() => expect(screen.getByTestId('multi-file-upload')).toBeInTheDocument());

        // Upload file using MultiFileUpload
        const fileInput = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        // Start upload manually
        const startUploadButton = await screen.findByTestId('start-upload-button');
        await waitFor(() => expect(startUploadButton).not.toBeDisabled());
        fireEvent.click(startUploadButton);

        // Wait for events instead of DOM polling (accounts for retry logic)
        await waitFor(() => expect(uploadEvents).toContain('upload_start'));
        await waitFor(() => expect(uploadEvents).toContain('upload_error'), { timeout: 10000 });

        // Then verify UI shows error with role="alert"
        const alerts = screen.queryAllByRole('alert');
        expect(alerts.length).toBeGreaterThan(0);
      });
    });

    describe('When upload request throws exception', () => {
      it('Then error message with exception details is displayed', async () => {
        // Session 9: Using observability hooks for reliable error state tracking
        const uploadEvents: string[] = [];
        const UploadPageWithHooks = () => (
          <UploadPage
            autoUpload={false}
            onUploadStart={() => uploadEvents.push('upload_start')}
            onUploadError={() => uploadEvents.push('upload_error')}
          />
        );

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

        render(<UploadPageWithHooks />);

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        const confirmButton = screen.getByRole('button', { name: /Confirm Game Selection/i });
        await waitFor(() => expect(confirmButton).not.toBeDisabled());
        fireEvent.click(confirmButton);

        // Wait for MultiFileUpload to appear
        await waitFor(() => expect(screen.getByTestId('multi-file-upload')).toBeInTheDocument());

        const fileInput = screen.getByLabelText(/File input for PDF upload/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
        fireEvent.change(fileInput, { target: { files: [file] } });

        // Start upload manually
        const startUploadButton = await screen.findByTestId('start-upload-button');
        await waitFor(() => expect(startUploadButton).not.toBeDisabled());
        fireEvent.click(startUploadButton);

        // Wait for events instead of DOM polling (accounts for retry logic)
        await waitFor(() => expect(uploadEvents).toContain('upload_start'));
        await waitFor(() => expect(uploadEvents).toContain('upload_error'), { timeout: 10000 });

        // Then verify UI shows error with role="alert"
        const alerts = screen.queryAllByRole('alert');
        expect(alerts.length).toBeGreaterThan(0);
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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        // Select a game first before confirming
        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        // Now wait for and click the confirm button
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        // Select a game first before confirming
        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        // Now wait for and click the confirm button
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        // Select a game first before confirming
        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        // Now wait for and click the confirm button
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        // Select a game first before confirming
        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        // Now wait for and click the confirm button
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

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

          // Wait for Shadcn Select trigger button
          const selectTrigger = await waitFor(() => {
            const trigger = screen.getByRole('combobox', { name: /select.*game/i });
            expect(trigger).toBeInTheDocument();
            return trigger;
          });

          // Open the select dropdown
          fireEvent.click(selectTrigger);

          // Wait for and click the game option by role
          const gameOption = await waitFor(() => {
            // Use getAllByText since it might appear multiple times
            const options = screen.getAllByText('Terraforming Mars');
            // Find the one that's clickable (not in the trigger)
            return options.find(opt => opt.closest('[role="option"]')) || options[options.length - 1];
          });
          fireEvent.click(gameOption);

          // Now wait for and click the confirm button
          const confirmButton = await waitFor(() => {
            const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
            expect(btn).not.toBeDisabled();
            return btn;
          });
          fireEvent.click(confirmButton);

          await waitFor(() => expect(screen.getByText(/failed.pdf/i)).toBeInTheDocument());

          const retryButton = screen.getByRole('button', { name: /Retry parsing/i });
          fireEvent.click(retryButton);

          await waitFor(() =>
            expect(screen.getByText(/✅ Parse re-triggered for failed.pdf/i)).toBeInTheDocument()
          );
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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        // Select a game first before confirming
        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        // Now wait for and click the confirm button
        const confirmButton = await waitFor(() => {
          const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
          expect(btn).not.toBeDisabled();
          return btn;
        });
        fireEvent.click(confirmButton);

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

        await waitFor(() => expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument());

        const gameSelect = screen.getByLabelText(/Select Game/i);
        fireEvent.change(gameSelect, { target: { value: 'game-1' } });

        const confirmButton = screen.getByRole('button', { name: /Confirm Game Selection/i });
        await waitFor(() => expect(confirmButton).not.toBeDisabled());
        fireEvent.click(confirmButton);

        // Wait for file input to be ready
        await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());

        // Wait for PDF upload form to appear
        await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());

        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'test.pdf', { type: 'application/pdf' });

        // First, verify the button exists (might be disabled)
        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });

        // Now change the file which should enable the button
        fireEvent.change(fileInput, { target: { files: [file] } });

        // Wait for button to become enabled
        await waitFor(() => expect(uploadButton).not.toBeDisabled());
        fireEvent.click(uploadButton);

        // Wait for automatic advance to parse step with polling
        await waitFor(() => {
          expect(screen.getByText(/Processing status/i)).toBeInTheDocument();
        }, { timeout: 5000 });

        // Wait for polling to start and error to be displayed
        // The polling will automatically retry after the first 500 error
        await waitFor(() => {
          expect(screen.getByText(/Status refresh failed/i)).toBeInTheDocument();
        }, { timeout: 10000 });

        // Verify that polling eventually succeeds after the error
        await waitFor(() => {
          expect(screen.getByText(/Processing status/i)).toHaveTextContent('Completed');
        }, { timeout: 10000 });
      }, 20000);
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