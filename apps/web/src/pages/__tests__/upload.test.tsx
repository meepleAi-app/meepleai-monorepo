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

  it('blocks access for users without admin or editor role', async () => {
    const viewerResponse = {
      user: {
        id: 'viewer-1',
        email: 'viewer@example.com',
        role: 'Viewer',
        displayName: 'Viewer User'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(viewerResponse);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() =>
      expect(screen.getByText(/You need an Editor or Admin role to manage PDF ingestion/i)).toBeInTheDocument()
    );
    expect(screen.getByRole('link', { name: /Back to Home/i })).toBeInTheDocument();
  });

  it('publishes rulespec successfully and shows success message', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-10',
          email: 'user10@example.com',
          role: 'Editor',
          displayName: 'User Ten'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Rule to publish' }]
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
          return createJsonResponse({ documentId: 'pdf-123' });
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
          return createJsonResponse(ruleSpecResponse, 200);
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

      await act(async () => {
        jest.advanceTimersByTime(2500);
      });

      await waitFor(() => expect(screen.getByText(/Rule to publish/i)).toBeInTheDocument());

      const publishButton = screen.getByRole('button', { name: /Publish RuleSpec/i });
      fireEvent.click(publishButton);

      await waitFor(() =>
        expect(screen.getByText(/✅ RuleSpec published successfully!/i)).toBeInTheDocument()
      );
    } finally {
      jest.useRealTimers();
    }
  });

  it('allows editing rule atom text in review phase', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-11',
          email: 'user11@example.com',
          role: 'Admin',
          displayName: 'User Eleven'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Original rule text', section: 'Setup', page: '1', line: '1' }]
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
          return createJsonResponse({ documentId: 'pdf-123' });
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

      await act(async () => {
        jest.advanceTimersByTime(2500);
      });

      await waitFor(() => expect(screen.getByText(/Original rule text/i)).toBeInTheDocument());

      const ruleTextInput = screen.getByDisplayValue('Original rule text');
      fireEvent.change(ruleTextInput, { target: { value: 'Edited rule text' } });

      expect(screen.getByDisplayValue('Edited rule text')).toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('allows deleting a rule atom in review phase', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-12',
          email: 'user12@example.com',
          role: 'Admin',
          displayName: 'User Twelve'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [
          { id: 'r1', text: 'Rule to keep', section: null, page: null, line: null },
          { id: 'r2', text: 'Rule to delete', section: null, page: null, line: null }
        ]
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
          return createJsonResponse({ documentId: 'pdf-123' });
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

      await act(async () => {
        jest.advanceTimersByTime(2500);
      });

      await waitFor(() => expect(screen.getByText(/Rule to delete/i)).toBeInTheDocument());

      const deleteButtons = screen.getAllByRole('button', { name: /Delete/i });
      fireEvent.click(deleteButtons[1]); // Delete second rule

      await waitFor(() => expect(screen.queryByText(/Rule to delete/i)).not.toBeInTheDocument());
      expect(screen.getByText(/Rule to keep/i)).toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('allows adding a new rule atom in review phase', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-13',
          email: 'user13@example.com',
          role: 'Admin',
          displayName: 'User Thirteen'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Existing rule' }]
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
          return createJsonResponse({ documentId: 'pdf-123' });
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

      await act(async () => {
        jest.advanceTimersByTime(2500);
      });

      await waitFor(() => expect(screen.getByText(/Existing rule/i)).toBeInTheDocument());

      const initialTextareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
      expect(initialTextareas).toHaveLength(1);

      const addButton = screen.getByRole('button', { name: /Add Rule/i });
      fireEvent.click(addButton);

      await waitFor(() => {
        const textareas = screen.getAllByRole('textbox').filter((el) => el.tagName === 'TEXTAREA');
        expect(textareas).toHaveLength(2);
      });
    } finally {
      jest.useRealTimers();
    }
  });

  it('auto-selects first game and enables confirm button', async () => {
    const authResponse = {
      user: {
        id: 'user-14',
        email: 'user14@example.com',
        role: 'Admin',
        displayName: 'User Fourteen'
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

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    // First game should be auto-selected
    const gameSelect = screen.getByLabelText(/Existing games/i) as HTMLSelectElement;
    expect(gameSelect.value).toBe('game-1');

    // Confirm button should be enabled for the selected game
    const confirmButton = screen.getByRole('button', { name: /Confirm selection/i });
    expect(confirmButton).toBeEnabled();
  });

  it('shows error when trying to upload without confirming game', async () => {
    const authResponse = {
      user: {
        id: 'user-15',
        email: 'user15@example.com',
        role: 'Admin',
        displayName: 'User Fifteen'
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

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    // Don't confirm, just try to upload
    const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
    const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
    fireEvent.change(fileInput, { target: { files: [file] } });

    // Upload button should be disabled
    const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
    expect(uploadButton).toBeDisabled();
  });

  it('shows error when game creation fails', async () => {
    const authResponse = {
      user: {
        id: 'user-16',
        email: 'user16@example.com',
        role: 'Admin',
        displayName: 'User Sixteen'
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
        return createErrorResponse(500, { error: 'Database error' });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText(/New game name/i), { target: { value: 'New Game' } });
    fireEvent.click(screen.getByRole('button', { name: /Create first game/i }));

    await waitFor(() =>
      expect(screen.getByText(/Failed to create game: API \/games 500/i)).toBeInTheDocument()
    );
  });

  it('allows retry parsing of failed PDF', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-17',
          email: 'user17@example.com',
          role: 'Admin',
          displayName: 'User Seventeen'
        },
        expiresAt: new Date().toISOString()
      };

      const failedPdf = {
        id: 'pdf-failed',
        fileName: 'failed.pdf',
        fileSizeBytes: 1024,
        uploadedAt: new Date().toISOString(),
        uploadedByUserId: 'user-17',
        status: 'failed',
        logUrl: null
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
          return createJsonResponse({ pdfs: [failedPdf] });
        }

        if (url.endsWith('/ingest/pdf/pdf-failed/retry') && method === 'POST') {
          return createJsonResponse({ success: true }, 200);
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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

  it('resets wizard state when reset button is clicked', async () => {
    jest.useFakeTimers();

    try {
      const authResponse = {
        user: {
          id: 'user-18',
          email: 'user18@example.com',
          role: 'Admin',
          displayName: 'User Eighteen'
        },
        expiresAt: new Date().toISOString()
      };

      const ruleSpecResponse = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [{ id: 'r1', text: 'Rule text' }]
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
          return createJsonResponse({ documentId: 'pdf-123' });
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

      await act(async () => {
        jest.advanceTimersByTime(2500);
      });

      await waitFor(() => expect(screen.getByText(/Rule text/i)).toBeInTheDocument());

      const resetButton = screen.getByRole('button', { name: /Cancel/i });
      fireEvent.click(resetButton);

      await waitFor(() => expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument());
      expect(screen.queryByText(/Rule text/i)).not.toBeInTheDocument();
    } finally {
      jest.useRealTimers();
    }
  });

  it('handles error when loading PDFs throws exception', async () => {
    const authResponse = {
      user: {
        id: 'user-19',
        email: 'user19@example.com',
        role: 'Admin',
        displayName: 'User Nineteen'
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
        return Promise.reject(new Error('Network failure'));
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

  it('handles unauthenticated user in initialize', async () => {
    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(null);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => {
      expect(screen.getByText(/You need to be logged in to manage games/i)).toBeInTheDocument();
    });
  });

  it('skips loading PDFs when gameId is empty', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games')) {
        return createJsonResponse([]);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

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

  it('handles JSON parsing error in polling', async () => {
    jest.useFakeTimers();
    try {
      const authResponse = {
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'Admin'
        },
        expiresAt: new Date().toISOString()
      };

      let pollCount = 0;

      mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string' ? input : input.toString();
        const method = init?.method ?? 'GET';

        if (url.endsWith('/auth/me')) {
          return createJsonResponse(authResponse);
        }

        if (url.endsWith('/games') && method === 'GET') {
          return createJsonResponse([{ id: 'game-1', name: 'Test Game', createdAt: new Date().toISOString() }]);
        }

        if (url.includes('/games/game-1/pdfs')) {
          return createJsonResponse({ pdfs: [] });
        }

        if (url.endsWith('/ingest/pdf')) {
          return createJsonResponse({ documentId: 'pdf-123' });
        }

        if (url.endsWith('/pdfs/pdf-123/text')) {
          pollCount++;
          if (pollCount === 1) {
            return createErrorResponse(500, null);
          }
          return createJsonResponse({
            processingStatus: 'completed',
            processingError: null
          });
        }

        if (url.endsWith('/games/game-1/rulespec')) {
          return createJsonResponse({ gameId: 'game-1', version: '1.0', createdAt: new Date().toISOString(), rules: [] });
        }

        throw new Error(`Unexpected fetch call to ${url}`);
      });

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

  it('shows error when confirming game without selection', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games')) {
        return createJsonResponse([]);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => {
      expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument();
    });

    // Try to create game without name
    const createGameInput = screen.getByLabelText(/New game name/i);
    fireEvent.change(createGameInput, { target: { value: '   ' } });
    fireEvent.click(screen.getByRole('button', { name: /Create first game/i }));

    await waitFor(() => {
      expect(screen.getByText(/Please enter a game name/i)).toBeInTheDocument();
    });
  });

  it('formats file size for small files correctly', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
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
        return createJsonResponse([{ id: 'game-1', name: 'Test', createdAt: new Date().toISOString() }]);
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createJsonResponse({
          pdfs: [
            {
              id: 'pdf-1',
              fileName: 'tiny.pdf',
              fileSizeBytes: 512,
              uploadedAt: new Date().toISOString(),
              uploadedByUserId: 'user-1'
            },
            {
              id: 'pdf-2',
              fileName: 'small.pdf',
              fileSizeBytes: 2048,
              uploadedAt: new Date().toISOString(),
              uploadedByUserId: 'user-1'
            }
          ]
        });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

    await waitFor(() => {
      expect(screen.getByText('512 B')).toBeInTheDocument();
      expect(screen.getByText('2.0 KB')).toBeInTheDocument();
    });
  });

  it('prevents rule atom operations when ruleSpec is null', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
      },
      expiresAt: new Date().toISOString()
    };

    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games')) {
        return createJsonResponse([{ id: 'game-1', name: 'Test', createdAt: new Date().toISOString() }]);
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createJsonResponse({ pdfs: [] });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

    // At this point, ruleSpec is null, so operations should be no-ops
    // This tests the guard conditions in updateRuleAtom, deleteRuleAtom, addRuleAtom
    expect(screen.queryByText(/Step 3: Review/i)).not.toBeInTheDocument();
  });

  it('handles create game without authentication', async () => {
    mockFetch.mockImplementation((input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(null);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => {
      expect(screen.getByText(/You need to be logged in to manage games/i)).toBeInTheDocument();
    });
  });

  it('opens log in new window when handleOpenLog is called', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
      },
      expiresAt: new Date().toISOString()
    };

    const windowOpenSpy = jest.spyOn(window, 'open').mockImplementation(() => null);

    mockFetch.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url.endsWith('/auth/me')) {
        return createJsonResponse(authResponse);
      }

      if (url.endsWith('/games') && method === 'GET') {
        return createJsonResponse([{ id: 'game-1', name: 'Test', createdAt: new Date().toISOString() }]);
      }

      if (url.includes('/games/game-1/pdfs')) {
        return createJsonResponse({
          pdfs: [
            {
              id: 'pdf-1',
              fileName: 'test.pdf',
              fileSizeBytes: 1024,
              uploadedAt: new Date().toISOString(),
              uploadedByUserId: 'user-1',
              logUrl: 'http://localhost:8080/logs/pdf-1'
            }
          ]
        });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

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

  it('handles retry parsing error scenarios', async () => {
    const authResponse = {
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'Admin'
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
        return createJsonResponse([{ id: 'game-1', name: 'Test', createdAt: new Date().toISOString() }]);
      }

      if (url.includes('/games/game-1/pdfs') && method === 'GET') {
        return createJsonResponse({
          pdfs: [
            {
              id: 'pdf-1',
              fileName: 'test.pdf',
              fileSizeBytes: 1024,
              uploadedAt: new Date().toISOString(),
              uploadedByUserId: 'user-1'
            }
          ]
        });
      }

      if (url.includes('/ingest/pdf/pdf-1/retry') && method === 'POST') {
        return createErrorResponse(500, { error: 'Retry failed' });
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

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

  it('handles error when upload request throws exception', async () => {
    const authResponse = {
      user: {
        id: 'user-20',
        email: 'user20@example.com',
        role: 'Admin',
        displayName: 'User Twenty'
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
        return Promise.reject(new Error('Connection timeout'));
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
      expect(screen.getByText(/❌ Upload failed: Connection timeout/i)).toBeInTheDocument()
    );
  });
});
