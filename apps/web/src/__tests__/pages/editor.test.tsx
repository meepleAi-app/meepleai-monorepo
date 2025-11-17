import {  screen, waitFor, fireEvent } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import RuleSpecEditor from '../../pages/editor';
import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';
import { waitForEditorReady, getEditorTextarea } from '../fixtures/test-helpers';

jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

describe('RuleSpecEditor (modern view)', () => {
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
        user: {
          id: 'user-1',
          email: 'editor@test.com',
          displayName: 'Test Editor',
          role: 'Editor',
        },
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

  it('shows login prompt when user is unauthenticated', async () => {
    router.get('/api/v1/auth/me', () => createErrorResponse(401));
    global.fetch = router.toMockImplementation();

    renderWithQuery(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText(/Devi effettuare l'accesso per utilizzare l'editor/i)).toBeInTheDocument();
    });
    expect(screen.getByRole('link', { name: /torna alla home/i })).toBeInTheDocument();
  });

  it('renders rule spec and allows switching to JSON view', async () => {
    renderWithQuery(<RuleSpecEditor />);
    await waitForEditorReady();

    expect(screen.getByText('Editor Visuale')).toBeInTheDocument();

    const textarea = getEditorTextarea();
    expect(textarea.value).toContain('game-1');
  });

  it('validates JSON and shows error state when content is invalid', async () => {
    renderWithQuery(<RuleSpecEditor />);
    await waitForEditorReady();

    const textarea = getEditorTextarea();
    fireEvent.change(textarea, { target: { value: '{ invalid json' } });
    fireEvent.blur(textarea);

    await waitFor(() => {
      expect(screen.getByText(/✗/)).toBeInTheDocument();
      expect(screen.getByText(/Expected property name/i)).toBeInTheDocument();
    });
  });

  it('saves changes and displays success status', async () => {
    const updatedSpec = {
      ...sampleRuleSpec,
      version: '1.0.1',
    };

    let capturedBody = '';

    router.put('/api/v1/games/game-1/rulespec', async (ctx) => {
      capturedBody = (ctx.init?.body as string) ?? '';
      return createJsonResponse(updatedSpec);
    });
    global.fetch = router.toMockImplementation();

    renderWithQuery(<RuleSpecEditor />);
    await waitForEditorReady();

    const textarea = getEditorTextarea();
    const updatedJson = JSON.stringify(updatedSpec, null, 2);

    fireEvent.change(textarea, { target: { value: updatedJson } });
    fireEvent.blur(textarea);

    const saveButton = screen.getByRole('button', { name: /salva ora/i });
    const user = userEvent.setup();
    await user.click(saveButton);

    await waitFor(() => {
      expect(capturedBody).toContain('"version":"1.0.1"');
      expect(screen.getByText(/RuleSpec salvato con successo/i)).toBeInTheDocument();
    });
  });
});
