import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import RuleSpecEditor from '../../pages/editor';
import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';
import { waitForEditorReady, getEditorTextarea } from '../fixtures/test-helpers';

jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Mock useDebounce to return immediately for faster tests
jest.mock('../../hooks/useDebounce', () => ({
  useDebounce: (value: any, _delay: number) => value,
}));

describe('RuleSpecEditor - Comprehensive Coverage (TEST-633)', () => {
  let router: MockApiRouter;
  const mockPush = jest.fn();
  const defaultRouter = {
    query: { gameId: 'game-1' },
    push: mockPush,
    pathname: '/editor',
  };

  const sampleRuleSpec = {
    gameId: 'game-1',
    version: '1.0.0',
    createdAt: '2025-01-15T10:00:00Z',
    rules: [
      { id: 'rule-1', text: 'Players take turns placing their mark' },
    ],
  };

  beforeEach(() => {
    router = new MockApiRouter();
    (useRouter as jest.Mock).mockReturnValue(defaultRouter);

    // Default auth success
    router.get('/api/v1/auth/me', () =>
      createJsonResponse({
        user: { id: 'user-1', email: 'editor@test.com', displayName: 'Test Editor', role: 'Editor' },
        expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      }),
    );

    // Default RuleSpec response
    router.get('/api/v1/games/game-1/rulespec', () => createJsonResponse(sampleRuleSpec));

    global.fetch = router.toMockImplementation();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Authentication', () => {
    it('shows login prompt when auth returns null (line 115)', async () => {
      router = new MockApiRouter();
      router.get('/api/v1/auth/me', () => createJsonResponse(null));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
      });
    });

    it('shows login prompt when unauthenticated', async () => {
      router.get('/api/v1/auth/me', () => createErrorResponse(401));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
      });
    });

    it('handles auth loading error (lines 115-118)', async () => {
      router = new MockApiRouter();
      router.get('/api/v1/auth/me', () => Promise.reject(new Error('Network error')));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
      });
    });

    it('shows permission denied for regular users', async () => {
      router = new MockApiRouter();
      router.get('/api/v1/auth/me', () =>
        createJsonResponse({
          user: { id: 'user-2', email: 'user@test.com', role: 'User' },
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
        }),
      );
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Non hai i permessi necessari/i)).toBeInTheDocument();
      });
    });

    it('allows access for Admin role', async () => {
      router = new MockApiRouter();
      router.get('/api/v1/auth/me', () =>
        createJsonResponse({
          user: { id: 'admin-1', email: 'admin@test.com', role: 'Admin' },
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
        }),
      );
      router.get('/api/v1/games/game-1/rulespec', () => createJsonResponse(sampleRuleSpec));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);
      await waitForEditorReady();

      expect(screen.getByText('Editor RuleSpec')).toBeInTheDocument();
    });

    it('shows missing gameId message', async () => {
      (useRouter as jest.Mock).mockReturnValue({ ...defaultRouter, query: {} });

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/Specifica un gameId/i)).toBeInTheDocument();
      });
    });
  });

  describe('RuleSpec Loading', () => {
    it('loads and displays rulespec', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      const content = JSON.parse(textarea.value);
      expect(content.gameId).toBe('game-1');
    });

    // Note: Error loading test removed - covered by editor.test.tsx

    it('shows error when rulespec not found (line 135)', async () => {
      router = new MockApiRouter();
      router.get('/api/v1/auth/me', () =>
        createJsonResponse({
          user: { id: 'user-1', email: 'editor@test.com', role: 'Editor' },
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
        }),
      );
      router.get('/api/v1/games/game-1/rulespec', () => createJsonResponse(null));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);

      await waitFor(() => {
        expect(screen.getByText(/RuleSpec non trovato/i)).toBeInTheDocument();
      });
    });
  });

  describe('JSON Validation (lines 78-97)', () => {
    beforeEach(async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();
    });

    it('validates missing gameId', async () => {
      const textarea = getEditorTextarea();
      const invalid = { ...sampleRuleSpec };
      delete (invalid as any).gameId;

      fireEvent.change(textarea, { target: { value: JSON.stringify(invalid) } });

      await waitFor(() => {
        expect(screen.getByText(/gameId è richiesto/i)).toBeInTheDocument();
      });
    });

    it('validates gameId type', async () => {
      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, gameId: 123 }) } });

      await waitFor(() => {
        expect(screen.getByText(/gameId.*stringa/i)).toBeInTheDocument();
      });
    });

    it('validates missing version', async () => {
      const textarea = getEditorTextarea();
      const invalid = { ...sampleRuleSpec };
      delete (invalid as any).version;

      fireEvent.change(textarea, { target: { value: JSON.stringify(invalid) } });

      await waitFor(() => {
        expect(screen.getByText(/version è richiesto/i)).toBeInTheDocument();
      });
    });

    it('validates version type', async () => {
      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, version: 1.0 }) } });

      await waitFor(() => {
        expect(screen.getByText(/version.*stringa/i)).toBeInTheDocument();
      });
    });

    it('validates missing createdAt', async () => {
      const textarea = getEditorTextarea();
      const invalid = { ...sampleRuleSpec };
      delete (invalid as any).createdAt;

      fireEvent.change(textarea, { target: { value: JSON.stringify(invalid) } });

      await waitFor(() => {
        expect(screen.getByText(/createdAt è richiesto/i)).toBeInTheDocument();
      });
    });

    it('validates createdAt type', async () => {
      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, createdAt: 123 }) } });

      await waitFor(() => {
        expect(screen.getByText(/createdAt.*stringa/i)).toBeInTheDocument();
      });
    });

    it('validates rules array', async () => {
      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, rules: 'invalid' }) } });

      await waitFor(() => {
        expect(screen.getByText(/rules deve essere un array/i)).toBeInTheDocument();
      });
    });

    it('validates rule.id type', async () => {
      const textarea = getEditorTextarea();
      const invalid = { ...sampleRuleSpec, rules: [{ id: 123, text: 'Test' }] };

      fireEvent.change(textarea, { target: { value: JSON.stringify(invalid) } });

      await waitFor(() => {
        expect(screen.getByText(/rules\[0\]\.id.*stringa/i)).toBeInTheDocument();
      });
    });

    it('validates rule.text type', async () => {
      const textarea = getEditorTextarea();
      const invalid = { ...sampleRuleSpec, rules: [{ id: 'r1', text: 456 }] };

      fireEvent.change(textarea, { target: { value: JSON.stringify(invalid) } });

      await waitFor(() => {
        expect(screen.getByText(/rules\[0\]\.text.*stringa/i)).toBeInTheDocument();
      });
    });
  });

  describe('Manual Save', () => {
    // Note: Manual save tests covered by editor.test.tsx

    it('disables save when invalid (lines 280-281)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: '{ bad }' } });

      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /Salva Ora/i });
        expect(saveButton).toBeDisabled();
      });
    });
  });

  describe('Auto-Save Coverage (lines 160-188)', () => {
    beforeEach(() => {
      router.put('/api/v1/games/game-1/rulespec', () =>
        createJsonResponse({ ...sampleRuleSpec, version: '1.0.1' }),
      );
      global.fetch = router.toMockImplementation();
    });

    it('triggers auto-save on content change (line 160)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      const updated = { ...sampleRuleSpec, version: '1.0.2' };

      fireEvent.change(textarea, { target: { value: JSON.stringify(updated, null, 2) } });

      // With immediate debounce mock, auto-save should trigger
      await waitFor(() => {
        expect(screen.getByText(/Auto-salvato/i)).toBeInTheDocument();
      }, { timeout: 5000 });
    });

    it('does not auto-save when invalid (line 166)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: '{ invalid }' } });

      // Wait and verify no save occurred
      await new Promise(resolve => setTimeout(resolve, 100));
      expect(screen.queryByText(/Auto-salvato/i)).not.toBeInTheDocument();
    });

    // Note: isSaving test removed - timing issues with mock

    it('clears hasUnsavedChanges after auto-save (line 179)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, version: '1.0.4' }, null, 2) } });

      await waitFor(() => {
        expect(screen.getByText(/Modifiche non salvate/i)).toBeInTheDocument();
      });

      await waitFor(() => {
        expect(screen.queryByText(/Modifiche non salvate/i)).not.toBeInTheDocument();
      }, { timeout: 5000 });
    });

    it('handles auto-save errors silently (lines 185-186)', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      router.put('/api/v1/games/game-1/rulespec', () => Promise.reject(new Error('Auto-save error')));
      global.fetch = router.toMockImplementation();

      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, version: '1.0.5' }, null, 2) } });

      await new Promise(resolve => setTimeout(resolve, 200));

      // Should not show error (auto-save errors are silent)
      expect(screen.queryByText(/Impossibile salvare/i)).not.toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });

    it('sets isSaving false in finally block (line 188)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      const textarea = getEditorTextarea();
      fireEvent.change(textarea, { target: { value: JSON.stringify({ ...sampleRuleSpec, version: '1.0.6' }, null, 2) } });

      await waitFor(() => {
        expect(screen.getByText(/Auto-salvato/i)).toBeInTheDocument();
      }, { timeout: 5000 });

      // After auto-save completes, button should show "Salvato" (not "Salvataggio...")
      await waitFor(() => {
        const saveButton = screen.getByRole('button', { name: /Salvato/i });
        expect(saveButton).toBeDisabled();
      });
    });
  });

  describe('View Mode Switching for Final Coverage', () => {
    it('switches from rich to JSON mode (covers line 206-209)', async () => {
      render(<RuleSpecEditor />);
      await waitForEditorReady();

      // Default is rich mode, switch to JSON
      const jsonButton = screen.getByRole('button', { name: /Codice JSON/i });
      const user = userEvent.setup();
      await user.click(jsonButton);

      await waitFor(() => {
        expect(screen.getByText('Editor JSON')).toBeInTheDocument();
      });

      // Switching back to rich triggers lines 210-214
      const richButton = screen.getByRole('button', { name: /Editor Visuale/i });
      await user.click(richButton);

      await waitFor(() => {
        expect(screen.getByText('Editor Visuale')).toBeInTheDocument();
      });
    });
  });
});
