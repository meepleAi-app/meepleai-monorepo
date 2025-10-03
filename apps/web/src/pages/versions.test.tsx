import { render, screen, waitFor } from '@testing-library/react';
import { useRouter } from 'next/router';
import VersionHistory from './versions';
import { api } from '../lib/api';

jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

jest.mock('../lib/api', () => ({
  api: {
    get: jest.fn(),
    put: jest.fn(),
  },
}));

describe('VersionHistory Page', () => {
  const mockRouter = {
    query: { gameId: 'demo-chess' },
    push: jest.fn(),
    pathname: '/versions',
    route: '/versions',
    asPath: '/versions?gameId=demo-chess',
    events: {
      on: jest.fn(),
      off: jest.fn(),
      emit: jest.fn(),
    },
    beforePopState: jest.fn(),
    prefetch: jest.fn(),
    back: jest.fn(),
    reload: jest.fn(),
    replace: jest.fn(),
    isReady: true,
    isLocaleDomain: false,
    isPreview: false,
  };

  const mockAuthUser = {
    id: 'user-1',
    tenantId: 'tenant-1',
    email: 'test@example.com',
    displayName: 'Test User',
    role: 'Admin',
  };

  const mockHistory = {
    gameId: 'demo-chess',
    totalVersions: 3,
    versions: [
      {
        version: 'v3',
        createdAt: '2025-10-03T12:00:00Z',
        ruleCount: 12,
        createdBy: 'user-1',
      },
      {
        version: 'v2',
        createdAt: '2025-10-02T12:00:00Z',
        ruleCount: 10,
        createdBy: 'user-1',
      },
      {
        version: 'v1',
        createdAt: '2025-10-01T12:00:00Z',
        ruleCount: 8,
        createdBy: 'user-1',
      },
    ],
  };

  const mockDiff = {
    gameId: 'demo-chess',
    fromVersion: 'v2',
    toVersion: 'v3',
    fromCreatedAt: '2025-10-02T12:00:00Z',
    toCreatedAt: '2025-10-03T12:00:00Z',
    summary: {
      totalChanges: 3,
      added: 2,
      modified: 1,
      deleted: 0,
      unchanged: 8,
    },
    changes: [
      {
        type: 'Added',
        newAtom: 'rule-11',
        newValue: {
          id: 'rule-11',
          text: 'New rule added',
          section: 'Setup',
          page: '5',
          line: '12',
        },
      },
      {
        type: 'Added',
        newAtom: 'rule-12',
        newValue: {
          id: 'rule-12',
          text: 'Another new rule',
          section: 'Gameplay',
          page: '6',
          line: '3',
        },
      },
      {
        type: 'Modified',
        oldAtom: 'rule-5',
        newAtom: 'rule-5',
        oldValue: {
          id: 'rule-5',
          text: 'Old text',
          section: 'Setup',
        },
        newValue: {
          id: 'rule-5',
          text: 'Updated text',
          section: 'Setup',
        },
        fieldChanges: [
          {
            fieldName: 'text',
            oldValue: 'Old text',
            newValue: 'Updated text',
          },
        ],
      },
    ],
  };

  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  it('renders unauthenticated state', async () => {
    (api.get as jest.Mock).mockResolvedValueOnce(null);

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText('Storico Versioni RuleSpec')).toBeInTheDocument();
      expect(screen.getByText(/Devi effettuare l'accesso/i)).toBeInTheDocument();
    });
  });

  it('renders version history when authenticated', async () => {
    (api.get as jest.Mock)
      .mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' })
      .mockResolvedValueOnce(mockHistory);

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Game:/)).toBeInTheDocument();
      expect(screen.getByText('demo-chess')).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText(/Versioni \(3\)/)).toBeInTheDocument();
      expect(screen.getByText('v3')).toBeInTheDocument();
      expect(screen.getByText('v2')).toBeInTheDocument();
      expect(screen.getByText('v1')).toBeInTheDocument();
    });
  });

  it('displays current version indicator', async () => {
    (api.get as jest.Mock)
      .mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' })
      .mockResolvedValueOnce(mockHistory);

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText('(corrente)')).toBeInTheDocument();
    });
  });

  it('loads and displays diff when versions are selected', async () => {
    (api.get as jest.Mock)
      .mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' })
      .mockResolvedValueOnce(mockHistory)
      .mockResolvedValueOnce(mockDiff);

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Confronta Versioni/)).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText(/Riepilogo Modifiche/)).toBeInTheDocument();
      expect(screen.getByText('+2')).toBeInTheDocument();
      expect(screen.getByText('~1')).toBeInTheDocument();
    });
  });

  it('shows restore buttons for non-current versions', async () => {
    (api.get as jest.Mock)
      .mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' })
      .mockResolvedValueOnce(mockHistory);

    render(<VersionHistory />);

    await waitFor(() => {
      const restoreButtons = screen.getAllByText('Ripristina');
      expect(restoreButtons).toHaveLength(3); // All 3 versions show button, but v3 (current) is disabled
      expect(restoreButtons[0]).toBeDisabled(); // v3 is disabled
      expect(restoreButtons[1]).not.toBeDisabled(); // v2 is enabled
      expect(restoreButtons[2]).not.toBeDisabled(); // v1 is enabled
    });
  });

  it('displays diff summary correctly', async () => {
    (api.get as jest.Mock)
      .mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' })
      .mockResolvedValueOnce(mockHistory)
      .mockResolvedValueOnce(mockDiff);

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText('Aggiunte')).toBeInTheDocument();
      expect(screen.getByText('Modificate')).toBeInTheDocument();
      expect(screen.getByText('Eliminate')).toBeInTheDocument();
      expect(screen.getByText('Non modificate')).toBeInTheDocument();
    });
  });

  it('handles missing gameId in query params', async () => {
    const routerWithoutGameId = { ...mockRouter, query: {} };
    (useRouter as jest.Mock).mockReturnValue(routerWithoutGameId);
    (api.get as jest.Mock).mockResolvedValueOnce({ user: mockAuthUser, expiresAt: '2025-10-04T12:00:00Z' });

    render(<VersionHistory />);

    await waitFor(() => {
      expect(screen.getByText(/Specifica un gameId/i)).toBeInTheDocument();
    });
  });
});
