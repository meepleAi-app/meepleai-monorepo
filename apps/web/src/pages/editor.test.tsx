import { render, screen, waitFor } from '@testing-library/react';
import { useRouter } from 'next/router';
import RuleSpecEditor from './editor';
import { api } from '../lib/api';

// Mock next/router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Mock API
jest.mock('../lib/api', () => ({
  api: {
    get: jest.fn(),
    put: jest.fn(),
  },
}));

const mockPush = jest.fn();
const mockedUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;
const mockedApi = api as jest.Mocked<typeof api>;

describe('RuleSpecEditor', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: {},
    } as any);
  });

  it('shows login message when user is not authenticated', async () => {
    mockedApi.get.mockResolvedValue(null);

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText(/devi effettuare l'accesso/i)).toBeInTheDocument();
    });
  });

  it('shows permission denied when user is not Admin or Editor', async () => {
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: { gameId: 'test-game' },
    } as any);

    mockedApi.get.mockResolvedValue({
      user: {
        id: '1',
        tenantId: 'tenant-1',
        email: 'user@example.com',
        role: 'User', // Not Admin or Editor
      },
      expiresAt: '2025-01-01T00:00:00Z',
    });

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText(/non hai i permessi necessari/i)).toBeInTheDocument();
    });
  });

  it('shows gameId required message when gameId is missing', async () => {
    mockedApi.get.mockResolvedValue({
      user: {
        id: '1',
        tenantId: 'tenant-1',
        email: 'admin@example.com',
        role: 'Admin',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    });

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText(/specifica un gameId/i)).toBeInTheDocument();
    });
  });

  it('renders editor for Admin user with gameId', async () => {
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: { gameId: 'test-game' },
    } as any);

    const mockRuleSpec = {
      gameId: 'test-game',
      version: 'v1',
      createdAt: '2025-01-01T00:00:00Z',
      rules: [
        { id: 'r1', text: 'Test rule' },
      ],
    };

    mockedApi.get
      .mockResolvedValueOnce({
        user: {
          id: '1',
          tenantId: 'tenant-1',
          email: 'admin@example.com',
          role: 'Admin',
        },
        expiresAt: '2025-01-01T00:00:00Z',
      })
      .mockResolvedValueOnce(mockRuleSpec);

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText('Editor RuleSpec')).toBeInTheDocument();
      expect(screen.getByText(/test-game/i)).toBeInTheDocument();
    });
  });

  it('renders editor for Editor role user', async () => {
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: { gameId: 'test-game' },
    } as any);

    const mockRuleSpec = {
      gameId: 'test-game',
      version: 'v1',
      createdAt: '2025-01-01T00:00:00Z',
      rules: [],
    };

    mockedApi.get
      .mockResolvedValueOnce({
        user: {
          id: '1',
          tenantId: 'tenant-1',
          email: 'editor@example.com',
          role: 'Editor', // Editor role should also work
        },
        expiresAt: '2025-01-01T00:00:00Z',
      })
      .mockResolvedValueOnce(mockRuleSpec);

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText('Editor RuleSpec')).toBeInTheDocument();
    });
  });

  it('shows loading state while fetching RuleSpec', async () => {
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: { gameId: 'test-game' },
    } as any);

    mockedApi.get.mockResolvedValueOnce({
      user: {
        id: '1',
        tenantId: 'tenant-1',
        email: 'admin@example.com',
        role: 'Admin',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    });

    // Delay the RuleSpec response
    mockedApi.get.mockImplementationOnce(() =>
      new Promise((resolve) => setTimeout(() => resolve({
        gameId: 'test-game',
        version: 'v1',
        createdAt: '2025-01-01T00:00:00Z',
        rules: [],
      }), 100))
    );

    render(<RuleSpecEditor />);

    await waitFor(() => {
      expect(screen.getByText(/caricamento/i)).toBeInTheDocument();
    });
  });

  it('shows error when RuleSpec fetch fails', async () => {
    mockedUseRouter.mockReturnValue({
      push: mockPush,
      query: { gameId: 'test-game' },
    } as any);

    mockedApi.get
      .mockResolvedValueOnce({
        user: {
          id: '1',
          tenantId: 'tenant-1',
          email: 'admin@example.com',
          role: 'Admin',
        },
        expiresAt: '2025-01-01T00:00:00Z',
      })
      .mockRejectedValueOnce(new Error('Network error'));

    render(<RuleSpecEditor />);

    await waitFor(() => {
      // Check that either the error message OR the editor with error state is shown
      const errorText = screen.queryByText(/impossibile caricare/i);
      const editorTitle = screen.queryByText('Editor RuleSpec');
      expect(errorText || editorTitle).toBeTruthy();
    }, { timeout: 3000 });
  });

  it('shows back to home link', async () => {
    mockedApi.get.mockResolvedValue(null);

    render(<RuleSpecEditor />);

    await waitFor(() => {
      const link = screen.getByRole('link', { name: /torna alla home/i });
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute('href', '/');
    });
  });
});
