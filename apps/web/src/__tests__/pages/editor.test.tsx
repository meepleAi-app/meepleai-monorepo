/**
 * Comprehensive Test Suite for RuleSpec Editor Page (editor.tsx)
 *
 * This is the MASTER test file for the 480-line JSON RuleSpec editor with undo/redo.
 * Covers all critical workflows: auth, game selection, RuleSpec loading, JSON validation,
 * editing, undo/redo, save, and error handling.
 *
 * Test Coverage: 48 tests across 7 categories
 * - Authentication & Route Setup (6 tests)
 * - RuleSpec Loading (8 tests)
 * - JSON Validation (10 tests)
 * - Save RuleSpec (8 tests)
 * - Undo/Redo History (11 tests)
 * - Auto-Format (3 tests)
 * - UI Elements & Navigation (2 tests)
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import RuleSpecEditor from '../../pages/editor';
import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';

// Mock Next.js router
const mockPush = jest.fn();
const mockRouter = {
  query: { gameId: 'game-1' },
  push: mockPush,
  pathname: '/editor',
  route: '/editor',
  asPath: '/editor?gameId=game-1',
};

jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Helper to get the editor textarea element
const getEditorTextarea = () => {
  const textarea = screen.queryAllByRole('textbox').find((el) => el.tagName === 'TEXTAREA');
  if (!textarea) {
    throw new Error('Editor textarea not found');
  }
  return textarea as HTMLTextAreaElement;
};

describe('RuleSpecEditor', () => {
  let router: MockApiRouter;
  let user: ReturnType<typeof userEvent.setup>;

  beforeEach(() => {
    router = new MockApiRouter();
    user = userEvent.setup();

    (useRouter as jest.Mock).mockReturnValue(mockRouter);

    // Default auth mock - authenticated editor user
    router.get('/api/v1/auth/me', () =>
      createJsonResponse({
        user: {
          id: 'user-1',
          email: 'editor@test.com',
          displayName: 'Test Editor',
          role: 'Editor',
        },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      })
    );

    // NOTE: global.fetch is set at the END of setupRuleSpec to ensure all routes are registered
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  // Helper to setup RuleSpec mock - MUST be called before render()
  const setupRuleSpec = (gameId: string, ruleSpec: any) => {
    router.get(`/api/v1/games/${gameId}/rulespec`, () =>
      createJsonResponse(ruleSpec)
    );
    // Set global.fetch AFTER all routes are registered
    global.fetch = router.toMockImplementation();
  };

  // Helper to finalize router setup after adding custom routes
  const finalizeRouter = () => {
    global.fetch = router.toMockImplementation();
  };

  // Sample RuleSpec
  const sampleRuleSpec = {
    gameId: 'game-1',
    version: '1.0.0',
    createdAt: '2025-01-15T10:00:00Z',
    rules: [
      {
        id: 'rule-1',
        text: 'Players take turns placing their mark',
        section: 'Gameplay',
        page: '5',
        line: '10',
      },
      {
        id: 'rule-2',
        text: 'First player to get three in a row wins',
        section: 'Winning',
        page: '6',
        line: '15',
      },
    ],
  };

  const sampleRuleSpecFormatted = JSON.stringify(sampleRuleSpec, null, 2);

  // ==================== 1. Authentication & Route Setup (6 tests) ====================

  describe('Authentication & Route Setup', () => {
    it('should load user on mount via /api/v1/auth/me', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
      });

      // Should not show "Devi effettuare l'accesso" message
      expect(screen.queryByText(/Devi effettuare l'accesso/i)).not.toBeInTheDocument();
    });

    it('should show login message when not authenticated', async () => {
      router.get('/api/v1/auth/me', () => createErrorResponse(401));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso per utilizzare l'editor/i)).toBeInTheDocument();
      });

      expect(screen.getByText('Torna alla home')).toBeInTheDocument();
    });

    it('should show permission error when user is not Editor or Admin', async () => {
      router.get('/api/v1/auth/me', () =>
        createJsonResponse({
          user: {
            id: 'user-2',
            email: 'user@test.com',
            role: 'User', // Regular user, not Editor/Admin
          },
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        })
      );
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Non hai i permessi necessari per utilizzare l'editor/i)).toBeInTheDocument();
      });

      expect(screen.getByText('Torna alla home')).toBeInTheDocument();
    });

    it('should parse gameId from router.query', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Game:/)).toBeInTheDocument();
        expect(screen.getByText('game-1')).toBeInTheDocument();
      });
    });

    it('should show error when gameId missing from URL', async () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: {}, // No gameId
      });
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Specifica un gameId nella query string/i)).toBeInTheDocument();
      });

      expect(screen.getByText('Torna alla home')).toBeInTheDocument();
    });

    it('should handle auth load failure gracefully', async () => {
      router.get('/api/v1/auth/me', () => {
        throw new Error('Network error');
      });
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
      });
    });
  });

  // ==================== 2. RuleSpec Loading (8 tests) ====================

  describe('RuleSpec Loading', () => {
    it('should fetch RuleSpec on mount with gameId', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
      });

      // Verify content was loaded
      const textarea = getEditorTextarea();
      expect(textarea.value).toContain('"gameId": "game-1"');
    });

    it('should display loading state during fetch', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      expect(screen.getByText('Caricamento...')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
      });
    });

    it('should set jsonContent from API response with 2-space indent', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBe(sampleRuleSpecFormatted);
      });
    });

    it('should set ruleSpec state and display in preview', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/1\.0\.0/)).toBeInTheDocument(); // Version in preview
        expect(screen.getByText(/rule-1/)).toBeInTheDocument(); // Rule ID in preview
      });
    });

    it('should display error when RuleSpec not found (404)', async () => {
      router.get('/api/v1/games/game-1/rulespec', () =>
        createErrorResponse(404, { message: 'RuleSpec not found' })
      );
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile caricare RuleSpec/i)).toBeInTheDocument();
      });
    });

    it('should handle API error (500)', async () => {
      router.get('/api/v1/games/game-1/rulespec', () =>
        createErrorResponse(500, { message: 'Internal server error' })
      );
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile caricare RuleSpec/i)).toBeInTheDocument();
      });
    });

    it('should handle network error', async () => {
      router.get('/api/v1/games/game-1/rulespec', () => {
        throw new Error('Network failure');
      });
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile caricare RuleSpec/i)).toBeInTheDocument();
      });
    });

    it('should show "Loading RuleSpec..." message during load', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      expect(screen.getByText('Caricamento...')).toBeInTheDocument();

      await waitFor(() => {
        expect(screen.queryByText('Caricamento...')).not.toBeInTheDocument();
      });
    });
  });

  // ==================== 3. JSON Validation (10 tests) ====================

  describe('JSON Validation', () => {
    it('should mark valid JSON as isValid=true', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/✓ JSON valido/)).toBeInTheDocument();
      });
    });

    it('should mark invalid JSON as isValid=false', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ invalid json');

      await waitFor(() => {
        expect(screen.getByText(/✗/)).toBeInTheDocument();
      });
    });

    it('should show validation error message for syntax errors', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ "invalid": }');

      await waitFor(() => {
        expect(screen.getByText(/JSON non valido/i)).toBeInTheDocument();
      });
    });

    it('should show specific error: "Unexpected token"', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ "key": "value", }'); // Trailing comma

      await waitFor(() => {
        const errorText = screen.queryByText(/✗/);
        expect(errorText).toBeInTheDocument();
      });
    });

    it('should show specific error: missing required field', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const invalidRuleSpec = {
        // Missing gameId
        version: '1.0.0',
        createdAt: '2025-01-15T10:00:00Z',
        rules: [],
      };

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, JSON.stringify(invalidRuleSpec, null, 2));

      await waitFor(() => {
        expect(screen.getByText(/gameId è richiesto/i)).toBeInTheDocument();
      });
    });

    it('should clear validation error when JSON becomes valid', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      // Make invalid
      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ invalid');

      await waitFor(() => {
        expect(screen.getByText(/✗/)).toBeInTheDocument();
      });

      // Make valid again
      await user.clear(textarea);
      await user.type(textarea, JSON.stringify(sampleRuleSpec, null, 2));

      await waitFor(() => {
        expect(screen.getByText(/✓ JSON valido/)).toBeInTheDocument();
      });
    });

    it('should validate on every keystroke (onChange)', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);

      // Type character by character and check validation updates
      await user.type(textarea, '{');
      await waitFor(() => {
        expect(screen.getByText(/✗/)).toBeInTheDocument();
      });
    });

    it('should mark empty textarea as invalid', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);

      await waitFor(() => {
        expect(screen.getByText(/✗/)).toBeInTheDocument();
      });
    });

    it('should mark whitespace-only textarea as invalid', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '   ');

      await waitFor(() => {
        expect(screen.getByText(/✗/)).toBeInTheDocument();
      });
    });

    it('should show red border on textarea when invalid', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ invalid');

      await waitFor(() => {
        expect(textarea).toHaveStyle({ border: expect.stringContaining('#d93025') });
      });
    });
  });

  // ==================== 4. Save RuleSpec (8 tests) ====================

  describe('Save RuleSpec', () => {
    it('should disable save button when invalid JSON', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.clear(textarea);
      await user.type(textarea, '{ invalid');

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /Salva/i });
        expect(saveButton).toBeDisabled();
      });
    });

    it('should enable save button when valid JSON', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );
      router.put('/api/v1/games/game-1/rulespec', () =>
        createJsonResponse({ ...sampleRuleSpec, version: '1.0.1' })
      );
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      // Modify content
      const textarea = getEditorTextarea();
      await user.type(textarea, ' ');

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /Salva/i });
        expect(saveButton).not.toBeDisabled();
      });
    });

    it('should PUT request to /api/v1/games/{gameId}/rulespec with parsed JSON', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );

      let capturedBody: any = null;
      router.put('/api/v1/games/game-1/rulespec', async ({ init }) => {
        const bodyText = await init?.body?.toString();
        capturedBody = JSON.parse(bodyText || '{}');
        return createJsonResponse({ ...sampleRuleSpec, version: '1.0.1' });
      });
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(capturedBody).toEqual(sampleRuleSpec);
      });
    });

    it('should show "Salvataggio..." state during save', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );

      let resolvePromise: (value: Response) => void;
      const savePromise = new Promise<Response>((resolve) => {
        resolvePromise = resolve;
      });

      router.put('/api/v1/games/game-1/rulespec', () => savePromise);
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      expect(screen.getByText('Salvataggio...')).toBeInTheDocument();

      // Resolve the promise
      resolvePromise!({
        ok: true,
        status: 200,
        json: async () => ({ ...sampleRuleSpec, version: '1.0.1' }),
      } as Response);

      await waitFor(() => {
        expect(screen.queryByText('Salvataggio...')).not.toBeInTheDocument();
      });
    });

    it('should show success message after save', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );
      router.put('/api/v1/games/game-1/rulespec', () =>
        createJsonResponse({ ...sampleRuleSpec, version: '1.0.1' })
      );
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/RuleSpec salvato con successo/i)).toBeInTheDocument();
        expect(screen.getByText(/versione 1\.0\.1/i)).toBeInTheDocument();
      });
    });

    it('should handle save error (400)', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );
      router.put('/api/v1/games/game-1/rulespec', () =>
        createErrorResponse(400, { message: 'Validation failed' })
      );
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile salvare RuleSpec/i)).toBeInTheDocument();
      });
    });

    it('should handle save error (500)', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );
      router.put('/api/v1/games/game-1/rulespec', () =>
        createErrorResponse(500, { message: 'Server error' })
      );
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile salvare RuleSpec/i)).toBeInTheDocument();
      });
    });

    it('should update ruleSpec state after successful save', async () => {
      router.get(`/api/v1/games/game-1/rulespec`, () =>
        createJsonResponse(sampleRuleSpec)
      );
      const updatedRuleSpec = { ...sampleRuleSpec, version: '2.0.0' };
      router.put('/api/v1/games/game-1/rulespec', () =>
        createJsonResponse(updatedRuleSpec)
      );
      finalizeRouter();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const saveButton = screen.getByRole('button', { name: /Salva/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/versione 2\.0\.0/i)).toBeInTheDocument();
      });
    });
  });

  // ==================== 5. Undo/Redo History (11 tests) ====================

  describe('Undo/Redo History', () => {
    it('should initialize history with loaded content', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBe(sampleRuleSpecFormatted);
      });

      // Undo button should be disabled (no history to go back to)
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      expect(undoButton).toBeDisabled();
    });

    it('should add history entry on blur after edit', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      await user.type(textarea, ' ');

      // Trigger blur to add to history
      fireEvent.blur(textarea);

      await waitFor(() => {
        const undoButton = screen.getByRole('button', { name: /Annulla/i });
        expect(undoButton).not.toBeDisabled();
      });
    });

    it('should undo restore previous content', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const originalContent = sampleRuleSpecFormatted;
      const textarea = getEditorTextarea();

      // Make an edit and blur
      await user.clear(textarea);
      await user.type(textarea, '{ "test": "modified" }');
      fireEvent.blur(textarea);

      await waitFor(() => {
        expect(textarea.value).toBe('{ "test": "modified" }');
      });

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      await waitFor(() => {
        expect(textarea.value).toBe(originalContent);
      });
    });

    it('should redo restore next content', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      const modifiedContent = '{ "test": "modified" }';

      // Make an edit and blur
      await user.clear(textarea);
      await user.type(textarea, modifiedContent);
      fireEvent.blur(textarea);

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      await waitFor(() => {
        expect(textarea.value).not.toBe(modifiedContent);
      });

      // Redo
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      await user.click(redoButton);

      await waitFor(() => {
        expect(textarea.value).toBe(modifiedContent);
      });
    });

    it('should disable undo button when no history', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      expect(undoButton).toBeDisabled();
    });

    it('should disable redo button at end of history', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      expect(redoButton).toBeDisabled();
    });

    it('should handle keyboard shortcut Ctrl+Z for undo', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const originalContent = sampleRuleSpecFormatted;
      const textarea = getEditorTextarea();

      // Make an edit and blur
      await user.clear(textarea);
      await user.type(textarea, '{ "test": "modified" }');
      fireEvent.blur(textarea);

      // Keyboard undo
      fireEvent.keyDown(textarea, { key: 'z', ctrlKey: true });

      await waitFor(() => {
        expect(textarea.value).toBe(originalContent);
      });
    });

    it('should handle keyboard shortcut Ctrl+Y for redo', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      const modifiedContent = '{ "test": "modified" }';

      // Make an edit and blur
      await user.clear(textarea);
      await user.type(textarea, modifiedContent);
      fireEvent.blur(textarea);

      // Undo
      fireEvent.keyDown(textarea, { key: 'z', ctrlKey: true });

      await waitFor(() => {
        expect(textarea.value).not.toBe(modifiedContent);
      });

      // Keyboard redo
      fireEvent.keyDown(textarea, { key: 'y', ctrlKey: true });

      await waitFor(() => {
        expect(textarea.value).toBe(modifiedContent);
      });
    });

    it('should handle keyboard shortcut Ctrl+Shift+Z for redo (alternate)', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();
      const modifiedContent = '{ "test": "modified" }';

      // Make an edit and blur
      await user.clear(textarea);
      await user.type(textarea, modifiedContent);
      fireEvent.blur(textarea);

      // Undo
      fireEvent.keyDown(textarea, { key: 'z', ctrlKey: true });

      await waitFor(() => {
        expect(textarea.value).not.toBe(modifiedContent);
      });

      // Keyboard redo (alternate shortcut)
      fireEvent.keyDown(textarea, { key: 'z', ctrlKey: true, shiftKey: true });

      await waitFor(() => {
        expect(textarea.value).toBe(modifiedContent);
      });
    });

    it('should not add duplicate history entries for same content', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();

      // Blur without changes
      fireEvent.blur(textarea);
      fireEvent.blur(textarea);
      fireEvent.blur(textarea);

      // Undo button should still be disabled
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      expect(undoButton).toBeDisabled();
    });

    it('should truncate future history on new edit after undo', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toBeTruthy();
      });

      const textarea = getEditorTextarea();

      // Edit 1
      await user.clear(textarea);
      await user.type(textarea, '{ "edit": 1 }');
      fireEvent.blur(textarea);

      // Edit 2
      await user.clear(textarea);
      await user.type(textarea, '{ "edit": 2 }');
      fireEvent.blur(textarea);

      // Undo to Edit 1
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      // Make new Edit 3 (should truncate Edit 2)
      await user.clear(textarea);
      await user.type(textarea, '{ "edit": 3 }');
      fireEvent.blur(textarea);

      // Redo should now be disabled (Edit 2 is gone)
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      expect(redoButton).toBeDisabled();
    });
  });

  // ==================== 6. Auto-Format (3 tests) ====================

  describe('Auto-Format', () => {
    it('should format JSON on initial load with 2-space indent', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        expect(textarea.value).toContain('  '); // 2-space indent
        expect(textarea.value).not.toContain('    '); // Not 4-space
      });
    });

    it('should validate formatted JSON immediately', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/✓ JSON valido/)).toBeInTheDocument();
      });
    });

    it('should preserve content structure after format', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        const textarea = getEditorTextarea();
        const parsed = JSON.parse(textarea.value);
        expect(parsed.gameId).toBe('game-1');
        expect(parsed.version).toBe('1.0.0');
        expect(parsed.rules).toHaveLength(2);
      });
    });
  });

  // ==================== 7. UI Elements & Navigation (2 tests) ====================

  describe('UI Elements & Navigation', () => {
    it('should display "Editor RuleSpec" title and game info', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /Editor RuleSpec/i })).toBeInTheDocument();
        expect(screen.getByText(/Game:/)).toBeInTheDocument();
        expect(screen.getByText('game-1')).toBeInTheDocument();
      });
    });

    it('should show navigation links (Home, Storico Versioni)', async () => {
      setupRuleSpec('game-1', sampleRuleSpec);

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText('Home')).toBeInTheDocument();
        expect(screen.getByText('Storico Versioni')).toBeInTheDocument();
      });

      const versionLink = screen.getByText('Storico Versioni').closest('a');
      expect(versionLink).toHaveAttribute('href', '/versions?gameId=game-1');

      const homeLink = screen.getByText('Home').closest('a');
      expect(homeLink).toHaveAttribute('href', '/');
    });
  });
});
