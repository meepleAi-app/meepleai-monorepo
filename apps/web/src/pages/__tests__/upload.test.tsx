import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../upload';

describe('UploadPage', () => {
  const originalFetch = global.fetch;
  const mockFetch = jest.fn();

  const createJsonResponse = (data: unknown, status = 200) =>
    Promise.resolve({
      ok: status >= 200 && status < 300,
      status,
      statusText: status >= 200 && status < 300 ? 'OK' : 'Error',
      json: () => Promise.resolve(data)
    } as Response);

  const createErrorResponse = (status: number, body: unknown = { error: 'Error' }, statusText?: string) =>
    Promise.resolve({
      ok: false,
      status,
      statusText:
        statusText ??
        (status === 401 ? 'Unauthorized' : status === 500 ? 'Internal Server Error' : 'Error'),
      json: () => Promise.resolve(body)
    } as Response);

  beforeEach(() => {
    mockFetch.mockReset();
    global.fetch = mockFetch as unknown as typeof fetch;
  });

  afterAll(() => {
    global.fetch = originalFetch;
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('keeps upload disabled until a game is confirmed', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin',
        displayName: 'User'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games') && method === 'GET') {
        return createJsonResponse([
          { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
        ]);
      }

      if (url.endsWith('/games') && method === 'POST') {
        return createJsonResponse({ id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() });
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createJsonResponse({ pdfs: [] });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
    const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
    const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

    fireEvent.change(fileInput, { target: { files: [file] } });
    expect(uploadButton).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

    await waitFor(() => expect(uploadButton).not.toBeDisabled());
  });

  it('allows creating a new game and enables upload once created', async () => {
    const authResponse = {
      user: {
        id: 'user-2',
        email: 'user2@example.com',
        role: 'Admin',
        displayName: 'User Two'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games') && method === 'GET') {
        return createJsonResponse([]);
      }

      if (url.endsWith('/games') && method === 'POST') {
        return createJsonResponse({ id: 'game-new', name: 'New Game', createdAt: new Date().toISOString() });
      }

      if (url.includes('/games/game-new/pdfs')) {
        return createJsonResponse({ pdfs: [] });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/New game name/i), { target: { value: 'New Game' } });
    fireEvent.click(screen.getByRole('button', { name: /Create first game/i }));

    await waitFor(() => expect(screen.getByRole('option', { name: 'New Game' })).toBeInTheDocument());

    const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
    const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
    const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
    fireEvent.change(fileInput, { target: { files: [file] } });

    await waitFor(() => expect(uploadButton).not.toBeDisabled());
  });

  it('polls PDF processing status and auto advances to review when completed', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-3',
          email: 'user3@example.com',
          role: 'Admin',
          displayName: 'User Three'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [
          { id: 'r1', text: 'Auto generated rule', section: 'Intro', page: '1', line: '1' }
        ]
      };

      const statusSequence: Array<{ processingStatus: string; processingError?: string | null }> = [
        { processingStatus: 'processing' },
        { processingStatus: 'completed' }
      ];

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          const nextStatus = statusSequence.shift() ?? { processingStatus: 'completed' };
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: nextStatus.processingStatus,
            processingError: nextStatus.processingError ?? null
          });
        }

        if (url.endsWith('/games/game-1/rulespec')) {
          return createJsonResponse(ruleSpecResponse);
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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

  it('displays an error when loading PDFs fails', async () => {
    const authResponse = {
      user: {
        id: 'user-4',
        email: 'user4@example.com',
        role: 'Admin',
        displayName: 'User Four'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games') && method === 'GET') {
        return createJsonResponse([
          { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
        ]);
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createErrorResponse(500, { error: 'Server error' });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

    await waitFor(() =>
      expect(screen.getByText(/Unable to load uploaded PDFs\. Please try again\./i)).toBeInTheDocument()
    );
  });

  it('shows parse failure message when polling reports a failed status', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-5',
          email: 'user5@example.com',
          role: 'Admin',
          displayName: 'User Five'
        },
        expiresAt: new Date().toISOString()
      };

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: 'failed',
            processingError: 'OCR error'
          });
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

      render(<UploadPage />);

      await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

      fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

      const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
      const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
      fireEvent.change(fileInput, { target: { files: [file] } });

      const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
      await waitFor(() => expect(uploadButton).not.toBeDisabled());
      fireEvent.click(uploadButton);

      await waitFor(() => expect(screen.getByText(/Processing status/i)).toBeInTheDocument());

      await waitFor(() =>
        expect(screen.getByText(/❌ Parse failed: OCR error/i)).toBeInTheDocument()
      );
      expect(screen.getByText(/Processing error: OCR error/i)).toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('shows polling error when status request fails', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-6',
          email: 'user6@example.com',
          role: 'Admin',
          displayName: 'User Six'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Rule text' }]
      };

      let statusCallCount = 0;

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          statusCallCount += 1;
          if (statusCallCount === 1) {
            return Promise.reject(new Error('Network down'));
          }
          if (statusCallCount === 2) {
            return createJsonResponse({
              id: 'pdf-123',
              fileName: 'rules.pdf',
              processingStatus: 'processing',
              processingError: null
            });
          }
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: 'completed',
            processingError: null
          });
        }

        if (url.endsWith('/games/game-1/rulespec')) {
          return createJsonResponse(ruleSpecResponse);
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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

  it('shows an error message when the upload request fails', async () => {
    const authResponse = {
      user: {
        id: 'user-7',
        email: 'user7@example.com',
        role: 'Admin',
        displayName: 'User Seven'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games') && method === 'GET') {
        return createJsonResponse([
          { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
        ]);
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createJsonResponse({ pdfs: [] });
      }

      if (url.endsWith('/ingest/pdf')) {
        return createErrorResponse(500, { error: 'Upload exploded' });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

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

  it('keeps the wizard on the parse step when the rulespec response is null', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-8',
          email: 'user8@example.com',
          role: 'Admin',
          displayName: 'User Eight'
        },
        expiresAt: new Date().toISOString()
      };

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: 'completed',
            processingError: null
          });
        }

        if (url.endsWith('/games/game-1/rulespec')) {
          return createErrorResponse(401, {});
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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
        expect(screen.getByText(/❌ Parse failed: Unable to load RuleSpec\./i)).toBeInTheDocument()
      );
      expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('shows an error message when handleParse throws', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-9',
          email: 'user9@example.com',
          role: 'Admin',
          displayName: 'User Nine'
        },
        expiresAt: new Date().toISOString()
      };

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: 'completed',
            processingError: null
          });
        }

        if (url.endsWith('/games/game-1/rulespec')) {
          return createErrorResponse(500, { error: 'Server exploded' });
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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
        expect(
          screen.getByText(/❌ Parse failed: API \/games\/game-1\/rulespec 500/i)
        ).toBeInTheDocument()
      );
      expect(screen.getByRole('heading', { name: /Step 2: Parse PDF/i })).toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('shows a failure message when publishing the rulespec fails', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-10',
          email: 'user10@example.com',
          role: 'Admin',
          displayName: 'User Ten'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Auto generated rule', section: 'Intro', page: '1', line: '1' }]
      };

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([
            { id: 'game-1', name: 'Terraforming Mars', createdAt: new Date().toISOString() }
          ]);
        }

        if (url.includes('/games/game-1/pdfs') && method === 'GET') {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123', fileName: 'rules.pdf' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          return createJsonResponse({
            id: 'pdf-123',
            fileName: 'rules.pdf',
            processingStatus: 'completed',
            processingError: null
          });
        }

        if (url.endsWith('/games/game-1/rulespec') && method === 'GET') {
          return createJsonResponse(ruleSpecResponse);
        }

        if (url.endsWith('/games/game-1/rulespec') && method === 'PUT') {
          return createErrorResponse(500, { error: 'Publish failed' });
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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
        expect(
          screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })
        ).toBeInTheDocument()
      );

      const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
      fireEvent.click(publishButton);

      await waitFor(() =>
        expect(screen.getByText(/❌ Publish failed: Publish failed/i)).toBeInTheDocument()
      );
      await waitFor(() =>
        expect(screen.getByRole('button', { name: /Publish RuleSpec/i })).toBeInTheDocument()
      );
    } finally {
      jest.useRealTimers();
    }
  });
});
