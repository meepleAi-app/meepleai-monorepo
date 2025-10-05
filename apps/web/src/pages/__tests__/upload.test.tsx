import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../upload';

describe('UploadPage', () => {
  const originalFetch = global.fetch;
  const mockFetch = jest.fn();

  const createJsonResponse = (data: unknown, status = 200) =>
    Promise.resolve({
      ok: status >= 200 && status < 300,
      status,
      json: () => Promise.resolve(data)
    } as Response);

  beforeEach(() => {
    mockFetch.mockReset();
    global.fetch = mockFetch as unknown as typeof fetch;
  });

  afterAll(() => {
    global.fetch = originalFetch;
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

  it('parses an uploaded PDF using the rulespec ingest endpoint', async () => {
    const authResponse = {
      user: {
        id: 'user-3',
        email: 'user3@example.com',
        role: 'Admin',
        displayName: 'User Three'
      },
      expiresAt: new Date().toISOString()
    };

    const parsedSpec = {
      gameId: 'game-parse',
      version: 'ingest-20240101000000',
      createdAt: new Date().toISOString(),
      rules: [
        {
          id: 'r1',
          text: 'Setup: Distribute four cards to each player',
          section: null,
          page: '4',
          line: null
        }
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
          { id: 'game-parse', name: 'Parse Game', createdAt: new Date().toISOString() }
        ]);
      }

      if (url.includes('/games/game-parse/pdfs')) {
        return createJsonResponse({ pdfs: [] });
      }

      if (url.endsWith('/ingest/pdf') && method === 'POST') {
        return createJsonResponse({ documentId: 'pdf-parse', fileName: 'rules.pdf' });
      }

      if (url.includes('/ingest/pdf/pdf-parse/rulespec') && method === 'POST') {
        return createJsonResponse(parsedSpec);
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

    await waitFor(() => expect(screen.getByText(/PDF uploaded successfully/i)).toBeInTheDocument());

    const parseButton = screen.getByRole('button', { name: /Parse PDF/i });
    await waitFor(() => expect(parseButton).not.toBeDisabled());
    fireEvent.click(parseButton);

    await waitFor(() => expect(screen.getByText(/PDF parsed successfully/i)).toBeInTheDocument());

    expect(screen.getByDisplayValue('Setup: Distribute four cards to each player')).toBeInTheDocument();
    expect(screen.getByDisplayValue('4')).toBeInTheDocument();
  });
});
