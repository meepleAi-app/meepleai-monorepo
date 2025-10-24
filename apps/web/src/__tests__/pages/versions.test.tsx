import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { NextRouter } from 'next/router';
import VersionHistory from '../../pages/versions';
import { api } from '../../lib/api';
import { useRouter } from 'next/router';
import { createMockAuthResponse, type MockAuthResponse } from '../fixtures/common-fixtures';
import { waitForApiCall } from '../fixtures/test-helpers';

// Mock the API client
jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    ruleSpecComments: {
      getComments: jest.fn(),
      createComment: jest.fn(),
      updateComment: jest.fn(),
      deleteComment: jest.fn()
    }
  }
}));

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

// Mock components
jest.mock('../../components/DiffViewer', () => ({
  DiffViewer: ({ diff, showOnlyChanges }: any) => {
    if (!diff) return null;
    return (
      <div data-testid="diff-viewer">
        <div data-testid="diff-from-version">{diff.fromVersion}</div>
        <div data-testid="diff-to-version">{diff.toVersion}</div>
        <div data-testid="diff-show-only-changes">{String(showOnlyChanges)}</div>
        {diff.summary && (
          <div data-testid="diff-summary">
            {diff.summary.added} added, {diff.summary.modified} modified, {diff.summary.deleted} deleted
          </div>
        )}
      </div>
    );
  },
}));

jest.mock('../../components/CommentThread', () => ({
  CommentThread: ({ gameId, version, currentUserId, currentUserRole }: any) => (
    <div data-testid="comment-thread">
      <div data-testid="comment-game-id">{gameId}</div>
      <div data-testid="comment-version">{version}</div>
      <div data-testid="comment-user-id">{currentUserId}</div>
      <div data-testid="comment-user-role">{currentUserRole}</div>
    </div>
  ),
}));

const mockApi = api as jest.Mocked<typeof api>;
const mockUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;

// Factory function to create router with gameId (no global state)
const createRouter = (gameId?: string, overrides: Partial<NextRouter> = {}): NextRouter =>
  ({
    route: '/versions',
    pathname: '/versions',
    query: gameId ? { gameId } : {},
    asPath: gameId ? `/versions?gameId=${gameId}` : '/versions',
    basePath: '',
    push: jest.fn(),
    replace: jest.fn(),
    reload: jest.fn(),
    back: jest.fn(),
    prefetch: jest.fn().mockResolvedValue(undefined),
    beforePopState: jest.fn(),
    events: {
      on: jest.fn(),
      off: jest.fn(),
      emit: jest.fn()
    },
    isFallback: false,
    isLocaleDomain: false,
    isReady: true,
    isPreview: false,
    defaultLocale: 'en',
    domainLocales: [],
    locale: undefined,
    ...overrides
  } as unknown as NextRouter);

// Test data fixtures
const authResponse = createMockAuthResponse({
  id: 'admin-1',
  email: 'admin@example.com',
  displayName: 'Admin User',
  role: 'Admin'
});

const editorAuthResponse = createMockAuthResponse({
  id: 'editor-1',
  email: 'editor@example.com',
  displayName: 'Editor User',
  role: 'Editor'
});

const userAuthResponse = createMockAuthResponse({
  id: 'user-1',
  email: 'user@example.com',
  displayName: 'Regular User',
  role: 'User'
});

const mockVersionHistory = {
  gameId: 'demo-chess',
  totalVersions: 3,
  versions: [
    { version: '3.0.0', createdAt: '2024-03-01T12:00:00Z', ruleCount: 15, createdBy: 'user-1' },
    { version: '2.0.0', createdAt: '2024-02-01T12:00:00Z', ruleCount: 12, createdBy: 'user-2' },
    { version: '1.0.0', createdAt: '2024-01-01T12:00:00Z', ruleCount: 10, createdBy: 'user-1' }
  ]
};

const mockDiffData = {
  gameId: 'demo-chess',
  fromVersion: '2.0.0',
  toVersion: '3.0.0',
  fromCreatedAt: '2024-02-01T12:00:00Z',
  toCreatedAt: '2024-03-01T12:00:00Z',
  summary: {
    totalChanges: 5,
    added: 3,
    modified: 1,
    deleted: 1,
    unchanged: 10
  },
  changes: [
    {
      type: 'Added',
      newAtom: 'rule-1',
      newValue: { id: 'rule-1', text: 'New rule', section: 'Setup', page: '1', line: '1' }
    },
    {
      type: 'Modified',
      oldAtom: 'rule-2',
      newAtom: 'rule-2',
      oldValue: { id: 'rule-2', text: 'Old text', section: 'Gameplay', page: '2', line: '5' },
      newValue: { id: 'rule-2', text: 'New text', section: 'Gameplay', page: '2', line: '5' },
      fieldChanges: [{ fieldName: 'text', oldValue: 'Old text', newValue: 'New text' }]
    },
    {
      type: 'Deleted',
      oldAtom: 'rule-3',
      oldValue: { id: 'rule-3', text: 'Deleted rule', section: 'Scoring', page: '3', line: '10' }
    }
  ]
};

const mockRuleSpec = {
  gameId: 'demo-chess',
  version: '2.0.0',
  createdAt: '2024-02-01T12:00:00Z',
  rules: [
    { id: 'rule-1', text: 'Rule 1', section: 'Setup', page: '1', line: '1' },
    { id: 'rule-2', text: 'Rule 2', section: 'Gameplay', page: '2', line: '5' }
  ]
};

let confirmSpy: jest.SpiedFunction<typeof window.confirm>;
let alertSpy: jest.SpiedFunction<typeof window.alert>;

beforeAll(() => {
  confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(false);
  alertSpy = jest.spyOn(window, 'alert').mockImplementation(() => {});
  mockUseRouter.mockImplementation(() => createRouter());
});

afterAll(() => {
  confirmSpy.mockRestore();
  alertSpy.mockRestore();
});

beforeEach(() => {
  jest.clearAllMocks();
  confirmSpy.mockReturnValue(false);
  alertSpy.mockImplementation(() => {});
  // Default router with no gameId
  mockUseRouter.mockImplementation(() => createRouter());
});

// =============================================================================
// TEST SUITE: VersionHistory Page (36+ comprehensive tests)
// =============================================================================

describe('VersionHistory Page', () => {
  // ===========================================================================
  // 1. AUTHENTICATION & ROUTE SETUP (4 tests)
  // ===========================================================================

  describe('Authentication & Route Setup', () => {
    it('loads user on mount', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitForApiCall(mockApi.get, '/api/v1/auth/me');
    });

    it('parses gameId from router.query', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitForApiCall(mockApi.get, '/api/v1/games/demo-chess/rulespec/history');

      expect(screen.getByText(/Game:/)).toBeInTheDocument();
      expect(screen.getByText('demo-chess')).toBeInTheDocument();
    });

    it('shows error when gameId missing', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);

      mockUseRouter.mockReturnValue(createRouter());
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Specifica un gameId nella query string/i)).toBeInTheDocument();
      });
    });

    it('handles auth failure', async () => {
      mockApi.get.mockResolvedValueOnce(null);

      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
      });

      expect(screen.getByText(/Torna alla home/i)).toBeInTheDocument();
    });
  });

  // ===========================================================================
  // 2. VERSION HISTORY LOADING (8 tests)
  // ===========================================================================

  describe('Version History Loading', () => {
    it('fetches history via /api/v1/games/{gameId}/rulespec/history', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitForApiCall(mockApi.get, '/api/v1/games/demo-chess/rulespec/history');
    });

    it('displays versions list with metadata', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText('3.0.0')).toBeInTheDocument();
      });

      expect(screen.getByText('2.0.0')).toBeInTheDocument();
      expect(screen.getByText('1.0.0')).toBeInTheDocument();
      expect(screen.getByText('15 regole')).toBeInTheDocument();
      expect(screen.getByText('12 regole')).toBeInTheDocument();
      expect(screen.getByText('10 regole')).toBeInTheDocument();
    });

    it('shows loading state during fetch', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Caricamento storico\.\.\./i)).toBeInTheDocument();
      });
    });

    it('handles empty history (no versions)', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce({
        gameId: 'demo-chess',
        versions: [],
        totalVersions: 0
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Versioni \(0\)/i)).toBeInTheDocument();
      });
    });

    it('handles API error (404)', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockRejectedValueOnce({ message: 'Game not found' });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Game not found/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });

    it('handles API error (500)', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockRejectedValueOnce({ message: 'Internal server error' });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Internal server error/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });

    it('displays version count "Total: X versions"', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Versioni \(3\)/i)).toBeInTheDocument();
      });
    });

    it('formats dates with toLocaleString()', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const expectedDate = new Date('2024-03-01T12:00:00Z').toLocaleString();
        expect(screen.getByText(expectedDate)).toBeInTheDocument();
      });
    });
  });

  // ===========================================================================
  // 3. VERSION SELECTION (8 tests)
  // ===========================================================================

  describe('Version Selection', () => {
    it('auto-selects latest two versions on load', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const fromSelect = screen.getByLabelText(/^Da versione:/i) as HTMLSelectElement;
        const toSelect = screen.getByLabelText(/^A versione:/i) as HTMLSelectElement;

        expect(fromSelect.value).toBe('2.0.0');
        expect(toSelect.value).toBe('3.0.0');
      });
    });

    it('changes version1 via dropdown', async () => {
      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData); // Initial auto-load
      mockApi.get.mockResolvedValueOnce(mockDiffData); // After manual selection

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByLabelText(/^Da versione:/i)).toBeInTheDocument();
      });

      const fromSelect = screen.getByLabelText(/^Da versione:/i);
      await user.selectOptions(fromSelect, '1.0.0');

      await waitFor(() => {
        expect((fromSelect as HTMLSelectElement).value).toBe('1.0.0');
      });
    });

    it('changes version2 via dropdown', async () => {
      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData); // Initial auto-load
      mockApi.get.mockResolvedValueOnce(mockDiffData); // After manual selection

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByLabelText(/^A versione:/i)).toBeInTheDocument();
      });

      const toSelect = screen.getByLabelText(/^A versione:/i);
      await user.selectOptions(toSelect, '2.0.0');

      await waitFor(() => {
        expect((toSelect as HTMLSelectElement).value).toBe('2.0.0');
      });
    });

    it('fetches diff when both versions selected', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitForApiCall(mockApi.get, '/api/v1/games/demo-chess/rulespec/diff?from=2.0.0&to=3.0.0');
    });

    it('displays selected version info (version number, date)', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const fromSelect = screen.getByLabelText(/^Da versione:/i);
        const options = Array.from((fromSelect as HTMLSelectElement).options);

        const versionOption = options.find(opt => opt.value === '2.0.0');
        expect(versionOption?.textContent).toContain('2.0.0');
        expect(versionOption?.textContent).toContain(new Date('2024-02-01T12:00:00Z').toLocaleString());
      });
    });

    it('shows "Loading diff..." during version fetches', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockImplementation((url) => {
        if (url.includes('/diff?')) {
          return new Promise(() => {}); // Never resolves
        }
        return Promise.resolve(mockVersionHistory);
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Caricamento diff\.\.\./i)).toBeInTheDocument();
      });
    });

    it('keeps diff displayed when version deselected (until new selection)', async () => {
      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-viewer')).toBeInTheDocument();
      });

      const fromSelect = screen.getByLabelText(/^Da versione:/i);
      await user.selectOptions(fromSelect, '');

      // Diff should still be visible (not cleared automatically)
      await waitFor(() => {
        expect(screen.getByTestId('diff-viewer')).toBeInTheDocument();
      });
    });

    it('shows message when no versions selected', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce({
        gameId: 'demo-chess',
        versions: [mockVersionHistory.versions[0]], // Only one version
        totalVersions: 1
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Seleziona due versioni per visualizzare le differenze/i)).toBeInTheDocument();
      });
    });
  });

  // ===========================================================================
  // 4. DIFF VIEWER INTEGRATION (6 tests)
  // ===========================================================================

  describe('Diff Viewer Integration', () => {
    it('renders DiffViewer component when both versions loaded', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-viewer')).toBeInTheDocument();
      });
    });

    it('passes diff data to DiffViewer', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-from-version')).toHaveTextContent('2.0.0');
        expect(screen.getByTestId('diff-to-version')).toHaveTextContent('3.0.0');
      });
    });

    it('displays change summary: "X added, Y modified, Z deleted"', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-summary')).toHaveTextContent('3 added, 1 modified, 1 deleted');
      });
    });

    it('keeps DiffViewer visible when one version deselected (until new selection)', async () => {
      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-viewer')).toBeInTheDocument();
      });

      const toSelect = screen.getByLabelText(/^A versione:/i);
      await user.selectOptions(toSelect, '');

      // Diff should still be visible (not cleared automatically)
      await waitFor(() => {
        expect(screen.getByTestId('diff-viewer')).toBeInTheDocument();
      });
    });

    it('passes showOnlyChanges prop to DiffViewer', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-show-only-changes')).toHaveTextContent('true');
      });
    });

    it('toggles showOnlyChanges when checkbox clicked', async () => {
      const user = userEvent.setup();
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-show-only-changes')).toHaveTextContent('true');
      });

      const checkbox = screen.getByLabelText(/Mostra solo modifiche/i);
      await user.click(checkbox);

      await waitFor(() => {
        expect(screen.getByTestId('diff-show-only-changes')).toHaveTextContent('false');
      });
    });
  });

  // ===========================================================================
  // 5. COMMENT THREAD (6 tests)
  // ===========================================================================

  describe('Comment Thread', () => {
    it('renders CommentThread component', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('comment-thread')).toBeInTheDocument();
      });
    });

    it('passes gameId to CommentThread', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('comment-game-id')).toHaveTextContent('demo-chess');
      });
    });

    it('passes version identifier to CommentThread', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('comment-version')).toHaveTextContent('3.0.0');
      });
    });

    it('passes currentUserId to CommentThread', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('comment-user-id')).toHaveTextContent('admin-1');
      });
    });

    it('passes currentUserRole to CommentThread', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockDiffData);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByTestId('comment-user-role')).toHaveTextContent('Admin');
      });
    });

    it('hides CommentThread when no version selected', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce({
        gameId: 'demo-chess',
        versions: [],
        totalVersions: 0
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.queryByTestId('comment-thread')).not.toBeInTheDocument();
      });
    });
  });

  // ===========================================================================
  // 6. UI ELEMENTS & NAVIGATION (4 tests)
  // ===========================================================================

  describe('UI Elements & Navigation', () => {
    it('displays "Storico Versioni RuleSpec" title', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText('Storico Versioni RuleSpec')).toBeInTheDocument();
      });
    });

    it('shows "Editor" link with gameId', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const editorLink = screen.getByText('Editor').closest('a');
        expect(editorLink).toHaveAttribute('href', '/editor?gameId=demo-chess');
      });
    });

    it('shows "Home" link', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const homeLink = screen.getByText('Home').closest('a');
        expect(homeLink).toHaveAttribute('href', '/');
      });
    });

    it('displays game identifier', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Game:/i)).toBeInTheDocument();
        expect(screen.getByText('demo-chess')).toBeInTheDocument();
      });
    });
  });

  // ===========================================================================
  // 7. VERSION RESTORATION (6 tests)
  // ===========================================================================

  describe('Version Restoration', () => {
    it('shows restore button for editors', async () => {
      mockApi.get.mockResolvedValueOnce(editorAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const restoreButtons = screen.getAllByText(/Ripristina/i);
        expect(restoreButtons.length).toBeGreaterThan(0);
      });
    });

    it('shows restore button for admins', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const restoreButtons = screen.getAllByText(/Ripristina/i);
        expect(restoreButtons.length).toBeGreaterThan(0);
      });
    });

    it('hides restore button for regular users', async () => {
      mockApi.get.mockResolvedValueOnce(userAuthResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.queryByText(/Ripristina/i)).not.toBeInTheDocument();
      });
    });

    it('disables restore button for current version', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const restoreButtons = screen.getAllByRole('button', { name: /Ripristina/i });
        // First version (current) should be disabled
        expect(restoreButtons[0]).toBeDisabled();
        // Older versions should be enabled
        expect(restoreButtons[1]).not.toBeDisabled();
      });
    });

    it('restores version on confirmation', async () => {
      const user = userEvent.setup();
      confirmSpy.mockReturnValue(true);

      mockApi.get.mockImplementation((url: string) => {
        if (url === '/api/v1/auth/me') {
          return Promise.resolve(authResponse);
        }
        if (url === '/api/v1/games/demo-chess/rulespec/history') {
          return Promise.resolve(mockVersionHistory);
        }
        if (url === '/api/v1/games/demo-chess/rulespec/versions/2.0.0') {
          return Promise.resolve(mockRuleSpec);
        }
        return Promise.resolve(null);
      });
      mockApi.put.mockResolvedValueOnce({ ...mockRuleSpec, version: '4.0.0' });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getAllByText(/Ripristina/i).length).toBeGreaterThan(0);
      });

      const restoreButtons = screen.getAllByRole('button', { name: /Ripristina/i });
      await user.click(restoreButtons[1]); // Click second button (first enabled)

      expect(confirmSpy).toHaveBeenCalledWith(
        expect.stringContaining('Sei sicuro di voler ripristinare la versione')
      );

      await waitForApiCall(mockApi.get, '/api/v1/games/demo-chess/rulespec/versions/2.0.0', 3000);

      await waitFor(() => {
        expect(mockApi.put).toHaveBeenCalledWith('/api/v1/games/demo-chess/rulespec', mockRuleSpec);
      }, { timeout: 3000 });
    });

    it('cancels restore on user rejection', async () => {
      const user = userEvent.setup();
      confirmSpy.mockReturnValue(false);

      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getAllByText(/Ripristina/i).length).toBeGreaterThan(0);
      });

      const restoreButtons = screen.getAllByRole('button', { name: /Ripristina/i });
      await user.click(restoreButtons[1]);

      await waitFor(() => {
        expect(confirmSpy).toHaveBeenCalled();
      });

      // Should not call API
      expect(mockApi.get).not.toHaveBeenCalledWith(expect.stringContaining('/versions/'));
      expect(mockApi.put).not.toHaveBeenCalled();
    });
  });

  // ===========================================================================
  // 8. ERROR HANDLING (4 tests)
  // ===========================================================================

  describe('Error Handling', () => {
    it('shows error when diff fetch fails', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockRejectedValueOnce({ message: 'Diff calculation failed' });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Diff calculation failed/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });

    it('shows error when restore fails', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      const user = userEvent.setup();
      confirmSpy.mockReturnValue(true);

      mockApi.get.mockImplementation((url: string) => {
        if (url === '/api/v1/auth/me') {
          return Promise.resolve(authResponse);
        }
        if (url === '/api/v1/games/demo-chess/rulespec/history') {
          return Promise.resolve(mockVersionHistory);
        }
        if (url === '/api/v1/games/demo-chess/rulespec/versions/2.0.0') {
          return Promise.reject({ message: 'Version not found' });
        }
        return Promise.resolve(null);
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getAllByText(/Ripristina/i).length).toBeGreaterThan(0);
      });

      const restoreButtons = screen.getAllByRole('button', { name: /Ripristina/i });
      await user.click(restoreButtons[1]);

      await waitFor(() => {
        expect(screen.getByText(/Version not found/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      consoleErrorSpy.mockRestore();
    });

    it('shows success message after restore', async () => {
      const user = userEvent.setup();
      confirmSpy.mockReturnValue(true);

      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);
      mockApi.get.mockResolvedValueOnce(mockRuleSpec);
      mockApi.put.mockResolvedValueOnce({ ...mockRuleSpec, version: '4.0.0' });
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getAllByText(/Ripristina/i).length).toBeGreaterThan(0);
      });

      const restoreButtons = screen.getAllByRole('button', { name: /Ripristina/i });
      await user.click(restoreButtons[1]);

      await waitFor(() => {
        expect(screen.getByText(/ripristinata con successo come versione 4\.0\.0/i)).toBeInTheDocument();
      });
    });

    it('handles history load error without message property', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockRejectedValueOnce({});

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText(/Impossibile caricare lo storico versioni/i)).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });
  });

  // ===========================================================================
  // 9. EDGE CASES (2 tests)
  // ===========================================================================

  describe('Edge Cases', () => {
    it('handles single version in history (no auto-selection)', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce({
        gameId: 'demo-chess',
        versions: [mockVersionHistory.versions[0]],
        totalVersions: 1
      });

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        const fromSelect = screen.getByLabelText(/^Da versione:/i) as HTMLSelectElement;
        const toSelect = screen.getByLabelText(/^A versione:/i) as HTMLSelectElement;

        expect(fromSelect.value).toBe('');
        expect(toSelect.value).toBe('');
      });
    });

    it('marks current version with (corrente) label', async () => {
      mockApi.get.mockResolvedValueOnce(authResponse);
      mockApi.get.mockResolvedValueOnce(mockVersionHistory);

      mockUseRouter.mockReturnValue(createRouter('demo-chess'));
      render(<VersionHistory />);

      await waitFor(() => {
        expect(screen.getByText('(corrente)')).toBeInTheDocument();
      });
    });
  });
});
