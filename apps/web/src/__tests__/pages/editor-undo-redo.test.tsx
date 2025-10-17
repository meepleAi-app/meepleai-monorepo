import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RuleSpecEditor from '../../pages/editor';
import { api } from '../../pages/../lib/api';
import { useRouter, type NextRouter } from 'next/router';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn()
  }
}));

jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

const mockApi = api as jest.Mocked<typeof api>;
const mockUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;

const createRouter = (overrides: Partial<NextRouter> = {}): NextRouter => {
  const router: NextRouter = {
    route: '/editor',
    pathname: '/editor',
    query: {},
    asPath: '/editor',
    basePath: '',
    push: jest
      .fn<ReturnType<NextRouter['push']>, Parameters<NextRouter['push']>>()
      .mockResolvedValue(true),
    replace: jest
      .fn<ReturnType<NextRouter['replace']>, Parameters<NextRouter['replace']>>()
      .mockResolvedValue(true),
    reload: jest.fn<ReturnType<NextRouter['reload']>, Parameters<NextRouter['reload']>>(),
    back: jest.fn<ReturnType<NextRouter['back']>, Parameters<NextRouter['back']>>(),
    forward: jest.fn<ReturnType<NextRouter['forward']>, Parameters<NextRouter['forward']>>(),
    prefetch: jest
      .fn<ReturnType<NextRouter['prefetch']>, Parameters<NextRouter['prefetch']>>()
      .mockResolvedValue(undefined),
    beforePopState: jest
      .fn<
        ReturnType<NextRouter['beforePopState']>,
        Parameters<NextRouter['beforePopState']>
      >(),
    isFallback: false,
    isLocaleDomain: false,
    isReady: true,
    isPreview: false,
    locale: undefined,
    locales: undefined,
    defaultLocale: undefined,
    domainLocales: undefined,
    events: {
      emit: jest.fn(),
      on: jest.fn(),
      off: jest.fn()
    },
    ...overrides
  };

  return router;
};

const getEditorTextarea = () => {
  const textarea = screen.queryAllByRole('textbox').find((el) => el.tagName === 'TEXTAREA');
  if (!textarea) {
    throw new Error('Editor textarea not found');
  }
  return textarea as HTMLTextAreaElement;
};

describe('RuleSpecEditor - Undo/Redo Functionality', () => {
  const authResponse = {
    user: {
      id: 'admin-1',
      email: 'admin@example.com',
      role: 'Admin'
    },
    expiresAt: new Date().toISOString()
  };

  const initialSpec = {
    gameId: 'demo-chess',
    version: '1.0.0',
    createdAt: new Date('2024-01-01').toISOString(),
    rules: [
      { id: 'rule-1', text: 'Initial rule text.' }
    ]
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseRouter.mockReturnValue(createRouter({ query: { gameId: 'demo-chess' } }));
    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec') {
        return initialSpec;
      }
      return null;
    });
  });

  describe('Undo functionality', () => {
    it('disables undo button when no history exists', async () => {
      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      expect(undoButton).toBeDisabled();
    });

    it('enables undo button after making edits and blur', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Make a change
      const modifiedSpec = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(modifiedSpec, null, 2) } });

      // Blur to add to history
      fireEvent.blur(textarea);

      await waitFor(() => {
        const undoButton = screen.getByRole('button', { name: /Annulla/i });
        expect(undoButton).toBeEnabled();
      });
    });

    it('restores previous content when undo is clicked', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();
      const originalContent = textarea.value;

      // Make first edit
      const modifiedSpec = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(modifiedSpec, null, 2) } });
      fireEvent.blur(textarea);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      // Should restore original content
      await waitFor(() => {
        expect(textarea.value).toBe(originalContent);
        expect(textarea.value).toContain('"version": "1.0.0"');
      });
    });

    it('allows multiple undo operations', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();
      const originalContent = textarea.value;

      // First edit
      const v1_1 = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v1_1, null, 2) } });
      fireEvent.blur(textarea);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      // Second edit
      const v1_2 = { ...initialSpec, version: '1.2.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v1_2, null, 2) } });
      fireEvent.blur(textarea);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.2.0"'));

      const undoButton = screen.getByRole('button', { name: /Annulla/i });

      // First undo - should go back to v1.1.0
      await user.click(undoButton);
      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      // Second undo - should go back to original (v1.0.0)
      await user.click(undoButton);
      await waitFor(() => {
        expect(textarea.value).toBe(originalContent);
        expect(textarea.value).toContain('"version": "1.0.0"');
      });

      // Undo button should be disabled now
      expect(undoButton).toBeDisabled();
    });

    it('preserves validation state after undo', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Make invalid edit
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: '{ invalid json' } });
      fireEvent.blur(textarea);

      await waitFor(() => expect(screen.getByText(/Expected property name/i)).toBeInTheDocument());

      // Undo to restore valid content
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      // Should show valid JSON indicator
      await waitFor(() => {
        expect(screen.getByText(/JSON valido/i)).toBeInTheDocument();
        expect(screen.queryByText(/Expected property name/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('Redo functionality', () => {
    it('disables redo button when no forward history exists', async () => {
      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      expect(redoButton).toBeDisabled();
    });

    it('enables redo button after undo', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Make edit
      const modifiedSpec = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(modifiedSpec, null, 2) } });
      fireEvent.blur(textarea);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.0.0"'));

      // Redo button should now be enabled
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      expect(redoButton).toBeEnabled();
    });

    it('restores undone content when redo is clicked', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Make edit
      const modifiedSpec = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(modifiedSpec, null, 2) } });
      fireEvent.blur(textarea);

      const modifiedContent = textarea.value;
      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);
      await waitFor(() => expect(textarea.value).toContain('"version": "1.0.0"'));

      // Redo
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      await user.click(redoButton);

      // Should restore the modified content
      await waitFor(() => {
        expect(textarea.value).toBe(modifiedContent);
        expect(textarea.value).toContain('"version": "1.1.0"');
      });
    });

    it('allows multiple redo operations', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // First edit
      const v1_1 = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v1_1, null, 2) } });
      fireEvent.blur(textarea);

      // Second edit
      const v1_2 = { ...initialSpec, version: '1.2.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v1_2, null, 2) } });
      fireEvent.blur(textarea);

      const finalContent = textarea.value;

      // Undo twice
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);
      await user.click(undoButton);

      await waitFor(() => expect(textarea.value).toContain('"version": "1.0.0"'));

      // Redo twice
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      await user.click(redoButton);
      await waitFor(() => expect(textarea.value).toContain('"version": "1.1.0"'));

      await user.click(redoButton);
      await waitFor(() => {
        expect(textarea.value).toBe(finalContent);
        expect(textarea.value).toContain('"version": "1.2.0"');
      });

      // Redo button should be disabled now
      expect(redoButton).toBeDisabled();
    });

    it('clears redo history when new edit is made', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Make first edit
      const v1_1 = { ...initialSpec, version: '1.1.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v1_1, null, 2) } });
      fireEvent.blur(textarea);

      // Undo
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      await user.click(undoButton);
      await waitFor(() => expect(textarea.value).toContain('"version": "1.0.0"'));

      // Redo button should be enabled
      const redoButton = screen.getByRole('button', { name: /Ripeti/i });
      expect(redoButton).toBeEnabled();

      // Make a new edit (different from the undone one)
      const v2_0 = { ...initialSpec, version: '2.0.0' };
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: JSON.stringify(v2_0, null, 2) } });
      fireEvent.blur(textarea);

      // Redo button should now be disabled (redo history cleared)
      await waitFor(() => {
        expect(redoButton).toBeDisabled();
      });
    });
  });

  describe('History edge cases', () => {
    it('does not add duplicate entries when content unchanged', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();
      const originalContent = textarea.value;

      // Focus and blur without changes
      await user.click(textarea);
      fireEvent.blur(textarea);

      // Make same content change
      await user.clear(textarea);
      fireEvent.change(textarea, { target: { value: originalContent } });
      fireEvent.blur(textarea);

      // Undo button should still be disabled (no new history entry)
      const undoButton = screen.getByRole('button', { name: /Annulla/i });
      expect(undoButton).toBeDisabled();
    });

    it('handles rapid edits with blur correctly', async () => {
      const user = userEvent.setup();

      render(<RuleSpecEditor />);

      await screen.findByText(/Editor RuleSpec/i);
      await waitFor(() => expect(getEditorTextarea().value).toContain('"version": "1.0.0"'));

      const textarea = getEditorTextarea();

      // Rapid edits with blur
      for (let i = 1; i <= 5; i++) {
        const spec = { ...initialSpec, version: `1.${i}.0` };
        await user.clear(textarea);
        fireEvent.change(textarea, { target: { value: JSON.stringify(spec, null, 2) } });
        fireEvent.blur(textarea);
      }

      // Should be able to undo all 5 changes
      const undoButton = screen.getByRole('button', { name: /Annulla/i });

      for (let i = 5; i >= 1; i--) {
        expect(undoButton).toBeEnabled();
        expect(textarea.value).toContain(`"version": "1.${i}.0"`);
        await user.click(undoButton);
      }

      // Final state should be original
      await waitFor(() => {
        expect(textarea.value).toContain('"version": "1.0.0"');
        expect(undoButton).toBeDisabled();
      });
    });
  });
});
