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

  it('parses an uploaded PDF using the backend and shows review data', async () => {
    const authResponse = {
      user: {
        id: 'user-3',
        email: 'user3@example.com',
        role: 'Editor',
        displayName: 'User Three'
      },
      expiresAt: new Date().toISOString()
    };

    const ruleSpecResponse = {
      gameId: 'game-1',
      version: 'v1.2.3',
      createdAt: new Date().toISOString(),
      rules: [
        { id: 'r-1', text: 'Always draw five cards.', section: 'Setup', page: '2', line: '10' }
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

      if (url.endsWith('/ingest/pdf') && method === 'POST') {
        return createJsonResponse({ documentId: 'doc-123', fileName: 'rules.pdf' });
      }

      if (url.endsWith('/ingest/pdf/parse') && method === 'POST') {
        return createJsonResponse(ruleSpecResponse);
      }

      throw new Error(`Unexpected fetch call to ${url}`);
    });

    render(<UploadPage />);

    await waitFor(() => expect(screen.getByRole('button', { name: /Confirm selection/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

    const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
    const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
    fireEvent.change(fileInput, { target: { files: [file] } });

    const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
    await waitFor(() => expect(uploadButton).not.toBeDisabled());

    fireEvent.click(uploadButton);

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Step 2: Parse PDF' })).toBeInTheDocument()
    );

    fireEvent.click(screen.getByRole('button', { name: /Parse PDF/i }));

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: /Step 3: Review & Edit Rules/i })).toBeInTheDocument()
    );

    const gameIdRow = screen.getByText(/Game ID:/i).closest('p');
    const versionRow = screen.getByText(/Version:/i).closest('p');
    expect(gameIdRow).toHaveTextContent('Game ID: game-1');
    expect(versionRow).toHaveTextContent('Version: v1.2.3');
    expect(screen.getByDisplayValue('Always draw five cards.')).toBeInTheDocument();

    const parseCall = mockFetch.mock.calls.find(([request]) => {
      const url = typeof request === 'string' ? request : request.toString();
      return url.endsWith('/ingest/pdf/parse');
    });

    expect(parseCall).toBeDefined();
    const [, parseInit] = parseCall!;
    expect(parseInit?.method).toBe('POST');
    expect(parseInit?.body).toBe(JSON.stringify({ gameId: 'game-1', documentId: 'doc-123' }));
  });
});
