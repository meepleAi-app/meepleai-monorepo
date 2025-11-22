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
import { UploadClient } from '@/app/upload/upload-client';
import {
  setupUploadMocks,
  createAuthMock,
  createGameMock
} from '../../pages/../__tests__/fixtures/upload-mocks';
import { getDefaultUserProps } from '../helpers/renderWithUser';

describe('UploadPage - Game Selection', () => {
  const originalFetch = global.fetch;
  let user: ReturnType<typeof userEvent.setup>;

  beforeEach(() => {
    jest.clearAllMocks();
    user = userEvent.setup();
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
      it('Then upload form is not shown', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        // Upload form should not be present until game is confirmed
        expect(screen.queryByRole('button', { name: /Upload PDF/i })).not.toBeInTheDocument();
        expect(screen.queryByLabelText(/PDF File/i)).not.toBeInTheDocument();
      });
    });

    describe('When user confirms game selection', () => {
      it('Then upload form appears with PDF input', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-1', role: 'Admin' }),
          games: [createGameMock({ id: 'game-1', name: 'Terraforming Mars' })],
          pdfs: { pdfs: [] }
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        await user.click(screen.getByRole('button', { name: /Confirm Game Selection/i }));

        // Wait for the upload form to appear with all elements
        await waitFor(() => {
          expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
          expect(screen.getByRole('button', { name: /Upload PDF/i })).toBeInTheDocument();
          expect(screen.getByLabelText(/Language/i)).toBeInTheDocument();
        });

        // Verify the form elements are present and correct
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        expect(fileInput.type).toBe('file');
        expect(fileInput.accept).toBe('application/pdf');
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

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => expect(screen.getByRole('combobox', { name: /select.*game/i })).toBeInTheDocument());

        // Wait for first game to be auto-selected
        await waitFor(() => {
          const gameSelect = screen.getByRole('combobox', { name: /select.*game/i });
          expect(gameSelect).toHaveTextContent('Terraforming Mars');
        });

        // Confirm button should be enabled for the selected game
        const confirmButton = screen.getByRole('button', { name: /Confirm Game Selection/i });
        expect(confirmButton).not.toBeDisabled();
      });
    });
  });

  describe('Given user has no games to select', () => {
    describe('When user creates a new game successfully', () => {
      it('Then new game appears in selection and upload form is shown', async () => {
        const mockFetch = setupUploadMocks({
          auth: createAuthMock({ userId: 'user-2', role: 'Admin' }),
          games: [],
          pdfs: { pdfs: [] },
          createGameResponse: createGameMock({ id: 'game-new', name: 'New Game' })
        });

        global.fetch = mockFetch as unknown as typeof fetch;

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => expect(screen.getByLabelText(/Create New Game/i)).toBeInTheDocument());

        await user.type(screen.getByPlaceholderText(/e.g., Gloomhaven/i), 'New Game');
        await user.click(screen.getByRole('button', { name: /Create/i }));

        // Wait for the new game to appear in the select dropdown
        await waitFor(() => {
          const selectTrigger = screen.getByRole('combobox', { name: /select.*game/i });
          expect(selectTrigger).toHaveTextContent('New Game');
        });

        // Confirm the game selection first
        await user.click(screen.getByRole('button', { name: /Confirm Game Selection/i }));

        // Wait for upload form to appear with all elements
        await waitFor(() => {
          expect(screen.getByLabelText(/PDF File/i)).toBeInTheDocument();
          expect(screen.getByRole('button', { name: /Upload PDF/i })).toBeInTheDocument();
          expect(screen.getByLabelText(/Language/i)).toBeInTheDocument();
        });

        // Verify the form elements are present
        const fileInput = screen.getByLabelText(/PDF File/i) as HTMLInputElement;
        expect(fileInput.type).toBe('file');
        expect(fileInput.accept).toBe('application/pdf');
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

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => expect(screen.getByLabelText(/Create New Game/i)).toBeInTheDocument());

        await user.type(screen.getByPlaceholderText(/e.g., Gloomhaven/i), 'New Game');
        await user.click(screen.getByRole('button', { name: /Create/i }));

        await waitFor(() =>
          expect(screen.getByText(/Database error/i)).toBeInTheDocument()
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

        render(<UploadClient {...getDefaultUserProps()} />);

        await waitFor(() => {
          expect(screen.getByLabelText(/Create New Game/i)).toBeInTheDocument();
        });

        // Try to create game without name - just click Create without typing anything
        const createButton = screen.getByRole('button', { name: /Create/i });

        // Button should be disabled initially
        expect(createButton).toBeDisabled();

        // Type spaces only and the button should remain disabled
        const createGameInput = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
        await user.type(createGameInput, '   ');

        // Button should still be disabled since trim() makes it empty
        expect(createButton).toBeDisabled();
      });
    });
  });
});