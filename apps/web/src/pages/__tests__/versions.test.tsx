import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { NextRouter } from 'next/router';
import VersionHistory from '../versions';
import { api } from '../../lib/api';
import { useRouter } from 'next/router';

type AuthResponse = {
  user: {
    id: string;
    email: string;
    role: string;
  };
  expiresAt: string;
};

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

let routerGameId: string | undefined;

const setGameId = (value?: string) => {
  routerGameId = value;
};

const createRouter = (overrides: Partial<NextRouter> = {}): NextRouter =>
  ({
    route: '/versions',
    pathname: '/versions',
    query: routerGameId ? { gameId: routerGameId } : {},
    asPath: routerGameId ? `/versions?gameId=${routerGameId}` : '/versions',
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

const authResponse: AuthResponse = {
  user: {
    id: 'admin-1',
    email: 'admin@example.com',
    role: 'Admin'
  },
  expiresAt: new Date().toISOString()
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
  setGameId(undefined);
  jest.clearAllMocks();
  confirmSpy.mockReturnValue(false);
  alertSpy.mockImplementation(() => {});
  mockUseRouter.mockImplementation(() => createRouter());
});

describe('VersionHistory page', () => {
  it('prompts for login when the current user is not authenticated', async () => {
    mockApi.get.mockResolvedValueOnce(null);

    render(<VersionHistory />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));
    expect(
      await screen.findByText(/Devi effettuare l'accesso per visualizzare lo storico/i)
    ).toBeInTheDocument();
  });

  it('asks for a gameId when authenticated but none is provided', async () => {
    mockApi.get.mockResolvedValueOnce(authResponse);

    render(<VersionHistory />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));
    expect(
      await screen.findByText(/Specifica un gameId nella query string/i)
    ).toBeInTheDocument();
  });

  it('auto-selects the most recent versions, loads the diff and supports toggling only changes', async () => {
    const user = userEvent.setup();
    setGameId('demo-chess');

    const historyResponse = {
      gameId: 'demo-chess',
      totalVersions: 3,
      versions: [
        { version: '2.0.0', createdAt: '2024-03-10T12:00:00Z', ruleCount: 12 },
        { version: '1.5.0', createdAt: '2024-02-15T12:00:00Z', ruleCount: 11 },
        { version: '1.0.0', createdAt: '2024-01-01T12:00:00Z', ruleCount: 10 }
      ]
    };

    const diffResponse = {
      gameId: 'demo-chess',
      fromVersion: '1.5.0',
      toVersion: '2.0.0',
      fromCreatedAt: '2024-02-15T12:00:00Z',
      toCreatedAt: '2024-03-10T12:00:00Z',
      summary: {
        totalChanges: 7,
        added: 2,
        modified: 1,
        deleted: 1,
        unchanged: 3
      },
      changes: [
        {
          type: 'Added',
          newAtom: 'Regola Bonus',
          newValue: { id: 'rule-3', text: 'Nuova regola' }
        },
        {
          type: 'Modified',
          oldAtom: 'Regola iniziale',
          fieldChanges: [
            { fieldName: 'text', oldValue: 'Vecchio testo', newValue: 'Nuovo testo' }
          ]
        },
        {
          type: 'Deleted',
          oldAtom: 'Regola rimossa',
          oldValue: { id: 'rule-2', text: 'Regola da rimuovere' }
        },
        {
          type: 'Unchanged',
          oldAtom: 'Regola invariata',
          oldValue: { id: 'rule-4', text: 'Nessuna modifica' }
        }
      ]
    };

    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/history') {
        return historyResponse;
      }
      if (path.startsWith('/api/v1/games/demo-chess/rulespec/diff')) {
        return diffResponse;
      }
      return null;
    });

    render(<VersionHistory />);

    await screen.findByText(/Riepilogo Modifiche/i);

    await waitFor(() =>
      expect(mockApi.get).toHaveBeenCalledWith(
        '/api/v1/games/demo-chess/rulespec/diff?from=1.5.0&to=2.0.0'
      )
    );

    const fromSelect = screen.getByLabelText(/^Da versione:/i) as HTMLSelectElement;
    const toSelect = screen.getByLabelText(/^A versione:/i) as HTMLSelectElement;

    await waitFor(() => {
      expect(fromSelect.value).toBe('1.5.0');
      expect(toSelect.value).toBe('2.0.0');
    });

    expect(screen.getByText('2.0.0')).toBeInTheDocument();
    expect(screen.getByText('1.5.0')).toBeInTheDocument();
    expect(screen.getByText('1.0.0')).toBeInTheDocument();

    const summary = screen.getByText(/Riepilogo Modifiche/i).parentElement;
    expect(summary).not.toBeNull();
    const summaryWithin = within(summary as HTMLElement);
    expect(summaryWithin.getByText('+2')).toBeInTheDocument();
    expect(summaryWithin.getByText('~1')).toBeInTheDocument();
    expect(summaryWithin.getByText('-1')).toBeInTheDocument();
    expect(summaryWithin.getByText('3')).toBeInTheDocument();

    expect(screen.getByText(/Modifiche \(3\)/i)).toBeInTheDocument();

    const showOnlyChangesToggle = screen.getByLabelText(/Mostra solo modifiche/i);
    expect(showOnlyChangesToggle).toBeChecked();

    await user.click(showOnlyChangesToggle);

    expect(showOnlyChangesToggle).not.toBeChecked();
    expect(screen.getByText(/Modifiche \(4\)/i)).toBeInTheDocument();
  });

  it('does not restore when the confirmation dialog is rejected', async () => {
    const user = userEvent.setup();
    setGameId('demo-chess');
    confirmSpy.mockReturnValue(false);

    const historyResponse = {
      gameId: 'demo-chess',
      totalVersions: 2,
      versions: [
        { version: '2.0.0', createdAt: '2024-03-10T12:00:00Z', ruleCount: 12 },
        { version: '1.5.0', createdAt: '2024-02-15T12:00:00Z', ruleCount: 11 }
      ]
    };

    const diffResponse = {
      gameId: 'demo-chess',
      fromVersion: '1.5.0',
      toVersion: '2.0.0',
      fromCreatedAt: '2024-02-15T12:00:00Z',
      toCreatedAt: '2024-03-10T12:00:00Z',
      summary: {
        totalChanges: 0,
        added: 0,
        modified: 0,
        deleted: 0,
        unchanged: 0
      },
      changes: []
    };

    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/history') {
        return historyResponse;
      }
      if (path.startsWith('/api/v1/games/demo-chess/rulespec/diff')) {
        return diffResponse;
      }
      return null;
    });

    render(<VersionHistory />);

    const restoreButtons = await screen.findAllByRole('button', { name: /Ripristina/i });
    await user.click(restoreButtons[1]);

    expect(confirmSpy).toHaveBeenCalledWith(
      'Sei sicuro di voler ripristinare la versione 1.5.0? Questo creerà una nuova versione.'
    );
    expect(mockApi.get).not.toHaveBeenCalledWith('/api/v1/games/demo-chess/rulespec/versions/1.5.0');
    expect(mockApi.put).not.toHaveBeenCalled();
  });

  it('restores a version when confirmed and surfaces success feedback', async () => {
    const user = userEvent.setup();
    setGameId('demo-chess');
    confirmSpy.mockReturnValue(true);

    const historyResponse = {
      gameId: 'demo-chess',
      totalVersions: 2,
      versions: [
        { version: '2.0.0', createdAt: '2024-03-10T12:00:00Z', ruleCount: 12 },
        { version: '1.5.0', createdAt: '2024-02-15T12:00:00Z', ruleCount: 11 }
      ]
    };

    const diffResponse = {
      gameId: 'demo-chess',
      fromVersion: '1.5.0',
      toVersion: '2.0.0',
      fromCreatedAt: '2024-02-15T12:00:00Z',
      toCreatedAt: '2024-03-10T12:00:00Z',
      summary: {
        totalChanges: 0,
        added: 0,
        modified: 0,
        deleted: 0,
        unchanged: 0
      },
      changes: []
    };

    const versionToRestore = {
      gameId: 'demo-chess',
      version: '1.5.0',
      createdAt: '2024-02-15T12:00:00Z',
      rules: [{ id: 'rule-1', text: 'Testo originale' }]
    };

    const restoredVersion = {
      ...versionToRestore,
      version: '3.0.0'
    };

    let historyCallCount = 0;
    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/history') {
        historyCallCount += 1;
        return historyResponse;
      }
      if (path.startsWith('/api/v1/games/demo-chess/rulespec/diff')) {
        return diffResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/versions/1.5.0') {
        return versionToRestore;
      }
      return null;
    });

    mockApi.put.mockResolvedValueOnce(restoredVersion);

    render(<VersionHistory />);

    const restoreButtons = await screen.findAllByRole('button', { name: /Ripristina/i });
    await user.click(restoreButtons[1]);

    expect(confirmSpy).toHaveBeenCalledWith(
      'Sei sicuro di voler ripristinare la versione 1.5.0? Questo creerà una nuova versione.'
    );

    await waitFor(() =>
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games/demo-chess/rulespec/versions/1.5.0')
    );
    expect(mockApi.put).toHaveBeenCalledWith('/api/v1/games/demo-chess/rulespec', versionToRestore);
    await screen.findByText(/Versione 1\.5\.0 ripristinata con successo come versione 3\.0\.0/i);
    expect(historyCallCount).toBeGreaterThanOrEqual(2);
  });

  it('shows an error message when restoring a version fails', async () => {
    const user = userEvent.setup();
    setGameId('demo-chess');
    confirmSpy.mockReturnValue(true);

    const historyResponse = {
      gameId: 'demo-chess',
      totalVersions: 2,
      versions: [
        { version: '2.0.0', createdAt: '2024-03-10T12:00:00Z', ruleCount: 12 },
        { version: '1.5.0', createdAt: '2024-02-15T12:00:00Z', ruleCount: 11 }
      ]
    };

    const diffResponse = {
      gameId: 'demo-chess',
      fromVersion: '1.5.0',
      toVersion: '2.0.0',
      fromCreatedAt: '2024-02-15T12:00:00Z',
      toCreatedAt: '2024-03-10T12:00:00Z',
      summary: {
        totalChanges: 0,
        added: 0,
        modified: 0,
        deleted: 0,
        unchanged: 0
      },
      changes: []
    };

    const versionToRestore = {
      gameId: 'demo-chess',
      version: '1.5.0',
      createdAt: '2024-02-15T12:00:00Z',
      rules: [{ id: 'rule-1', text: 'Testo originale' }]
    };

    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/history') {
        return historyResponse;
      }
      if (path.startsWith('/api/v1/games/demo-chess/rulespec/diff')) {
        return diffResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec/versions/1.5.0') {
        return versionToRestore;
      }
      return null;
    });

    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.put.mockRejectedValueOnce(new Error('Ripristino fallito'));

    render(<VersionHistory />);

    const restoreButtons = await screen.findAllByRole('button', { name: /Ripristina/i });
    await user.click(restoreButtons[1]);

    await waitFor(() => expect(mockApi.put).toHaveBeenCalled());
    await screen.findByText(/Ripristino fallito/i);

    consoleErrorSpy.mockRestore();
  });

  it('handles loadCurrentUser error gracefully', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.get.mockRejectedValueOnce(new Error('Auth failed'));

    setGameId('test-game');

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
    });

    consoleErrorSpy.mockRestore();
  });

  it('handles history load error without message property', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.get.mockResolvedValueOnce({ user: { id: '1', email: 'test@example.com', role: 'User' } });
    mockApi.get.mockRejectedValueOnce({});

    setGameId('test-game');

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Impossibile caricare lo storico versioni/i)).toBeInTheDocument();
    });

    consoleErrorSpy.mockRestore();
  });

  it('handles diff load error without message property', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    mockApi.get.mockResolvedValueOnce({ user: { id: '1', email: 'test@example.com', role: 'User' } });
    mockApi.get.mockResolvedValueOnce({
      gameId: 'test-game',
      versions: [
        { version: 'v2', createdAt: '2024-01-02T00:00:00Z', rulesCount: 10 },
        { version: 'v1', createdAt: '2024-01-01T00:00:00Z', rulesCount: 8 }
      ]
    });
    mockApi.get.mockRejectedValueOnce({});

    setGameId('test-game');

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Impossibile caricare il diff/i)).toBeInTheDocument();
    });

    consoleErrorSpy.mockRestore();
  });
});
