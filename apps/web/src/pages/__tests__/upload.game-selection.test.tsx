/**
 * Upload Page - Game Selection Workflow Tests
 *
 * BDD Scenarios:
 * - Game selection and confirmation
 * - Creating new games
 * - Game selection validation
 * - Authorization checks
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import UploadPage from '../upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock
} from '../../__tests__/fixtures/upload-mocks';

describe('UploadPage - Game Selection', () => {
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

  describe('Given user has existing games', () => {
    describe('When user selects game but does not confirm', () => {
      it('Then upload button remains disabled', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

        fireEvent.change(fileInput, { target: { files: [file] } });
        expect(uploadButton).toBeDisabled();
      });
    });

    describe('When user confirms game selection', () => {
      it('Then upload button becomes enabled with file selected', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Confirm selection/i }));

        const uploadButton = screen.getByRole('button', { name: /Upload & Continue/i });
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

        fireEvent.change(fileInput, { target: { files: [file] } });

        await waitFor(() => expect(uploadButton).not.toBeDisabled());
      });
    });

    describe('When first game is auto-selected', () => {
      it('Then confirm button is enabled', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-14', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByLabelText(/Existing games/i)).toBeInTheDocument());

        // First game should be auto-selected
        const gameSelect = screen.getByLabelText(/Existing games/i) as HTMLSelectElement;
        expect(gameSelect.value).toBe('game-1');

        // Confirm button should be enabled for the selected game
        const confirmButton = screen.getByRole('button', { name: /Confirm selection/i });
        expect(confirmButton).toBeEnabled();
      });
    });
  });

  describe('Given user has no existing games', () => {
    describe('When user creates a new game successfully', () => {
      it('Then new game appears in selection and upload is enabled', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-2', role: 'Admin' }),
          games: [],
          pdfs: { pdfs: [] },
          createGameResponse: createGameMock({ id: 'game-new', name: 'New Game' })
        });

        global.fetch = mockFetch as unknown as typeof fetch;

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
    });

    describe('When game creation fails', () => {
      it('Then error message is displayed', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-16', role: 'Admin' }),
          games: [],
          createGameError: { status: 500, error: 'Database error' }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument());

        fireEvent.change(screen.getByLabelText(/New game name/i), { target: { value: 'New Game' } });
        fireEvent.click(screen.getByRole('button', { name: /Create first game/i }));

        await waitFor(() =>
          expect(screen.getByText(/Failed to create game: API \/api\/v1\/games 500/i)).toBeInTheDocument()
        );
      });
    });

    describe('When user tries to create game without name', () => {
      it('Then validation error is displayed', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: []
        });

        global.fetch = mockFetch as unknown as typeof fetch;

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
    });
  });
});
