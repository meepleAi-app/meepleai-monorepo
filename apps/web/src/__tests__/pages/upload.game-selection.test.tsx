/**
 * Upload Page - Game Selection Workflow Tests
 *
 * BDD Scenarios:
 * - Game selection and confirmation
 * - Creating new games
 * - Game selection validation
 * - Authorization checks
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import UploadPage from '../../pages/upload';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock
} from '../../pages/../__tests__/fixtures/upload-mocks';

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

  // Helper for Shadcn Select interaction
  async function selectGame(gameId: string) {
    // Open the select dropdown
    const selectTrigger = screen.getByRole('combobox', { name: /select.*game/i });
    await user.click(selectTrigger);

    // Find and click the specific game option
    const gameOption = screen.getByRole('option', { name: new RegExp(gameId.replace('game-', 'Game '), 'i') });
    await user.click(gameOption);
  }

  // Helper to confirm game selection
  async function confirmGameSelection() {
    const confirmButton = await waitFor(() => {
      const btn = screen.getByRole('button', { name: /Confirm Game Selection/i });
      expect(btn).not.toBeDisabled();
      return btn;
    });
    await user.click(confirmButton);
  }

  describe('Given user has Select Game options', () => {
    describe('When user selects game but does not confirm', () => {
      it('Then upload button remains disabled', async () => {
        const user = userEvent.setup();
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

        await user.upload(fileInput, file);
        expect(uploadButton).toBeDisabled();
      });
    });

    describe('When user confirms game selection', () => {
      it('Then upload button becomes enabled with file selected', async () => {
        const user = userEvent.setup();
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await user.click(screen.getByRole('button', { name: /Confirm Game Selection/i }));

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });

        await user.upload(fileInput, file);

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

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        // First game should be auto-selected and shown in the combobox text
        const gameSelect = screen.getByRole('combobox', { name: /select.*game/i });
        expect(gameSelect).toHaveTextContent('Terraforming Mars');

        // Confirm button should be enabled for the selected game
        const confirmButton = screen.getByRole('button', { name: /Confirm Game Selection/i });
        expect(confirmButton).not.toBeDisabled();
      });
    });
  });

  describe('Given user has no games to select', () => {
    describe('When user creates a new game successfully', () => {
      it('Then new game appears in selection and upload is enabled', async () => {
        const user = userEvent.setup();
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-2', role: 'Admin' }),
          games: [],
          pdfs: { pdfs: [] },
          createGameResponse: createGameMock({ id: 'game-new', name: 'New Game' })
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument());

        await user.type(screen.getByLabelText(/New game name/i), 'New Game');
        await user.click(screen.getByRole('button', { name: /Create first game/i }));

        await waitFor(() => expect(screen.getByRole('option', { name: 'New Game' })).toBeInTheDocument());

        const uploadButton = screen.getByRole('button', { name: /Upload PDF/i });
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        const file = new File(['pdf'], 'rules.pdf', { type: 'application/pdf' });
        await user.upload(fileInput, file);

        await waitFor(() => expect(uploadButton).not.toBeDisabled());
      });
    });

    describe('When game creation fails', () => {
      it('Then error message is displayed', async () => {
        const user = userEvent.setup();
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-16', role: 'Admin' }),
          games: [],
          createGameError: { status: 500, error: 'Database error' }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadPage />);

        await waitFor(() => expect(screen.getByText(/Create one to get started/i)).toBeInTheDocument());

        await user.type(screen.getByLabelText(/New game name/i), 'New Game');
        await user.click(screen.getByRole('button', { name: /Create first game/i }));

        await waitFor(() =>
          expect(screen.getByText(/Failed to create game: API \/api\/v1\/games 500/i)).toBeInTheDocument()
        );
      });
    });

    describe('When user tries to create game without name', () => {
      it('Then validation error is displayed', async () => {
        const user = userEvent.setup();
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
        await user.type(createGameInput, '   ');
        await user.click(screen.getByRole('button', { name: /Create first game/i }));

        await waitFor(() => {
          expect(screen.getByText(/Please enter a game name/i)).toBeInTheDocument();
        });
      });
    });
  });
});