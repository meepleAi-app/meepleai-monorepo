import { render, screen, waitFor } from '@testing-library/react';
import { VersionTimeline } from '../../components/versioning/VersionTimeline';

// Mock next/navigation (App Router)
jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: jest.fn(),
    back: jest.fn(),
    forward: jest.fn(),
    refresh: jest.fn(),
    prefetch: jest.fn(),
  }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
}));

// Mock fetch
global.fetch = jest.fn();

describe('VersionTimeline', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render loading state initially', () => {
    (global.fetch as jest.Mock).mockImplementation(() =>
      new Promise(() => {}) // Never resolves
    );

    render(<VersionTimeline gameId="test-game" />);

    expect(screen.getByRole('status', { hidden: true })).toBeInTheDocument();
  });

  it('should fetch and display timeline data', async () => {
    const mockData = {
      versions: [
        {
          id: '1',
          version: 'v1',
          title: 'Version v1',
          description: '1 rule atoms',
          author: 'Test User',
          createdAt: '2024-01-01T00:00:00Z',
          changeCount: 1,
          isCurrentVersion: false,
        },
        {
          id: '2',
          version: 'v2',
          title: 'Version v2',
          description: '2 rule atoms',
          author: 'Test User',
          createdAt: '2024-01-02T00:00:00Z',
          parentVersionId: '1',
          parentVersion: 'v1',
          changeCount: 2,
          isCurrentVersion: true,
        },
      ],
    };

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => mockData,
    });

    render(<VersionTimeline gameId="test-game" />);

    await waitFor(() => {
      expect(screen.getByText('Version Timeline')).toBeInTheDocument();
    });
  });

  it('should display error state when fetch fails', async () => {
    (global.fetch as jest.Mock).mockRejectedValueOnce(new Error('Network error'));

    render(<VersionTimeline gameId="test-game" />);

    await waitFor(() => {
      expect(screen.getByText(/Error:/)).toBeInTheDocument();
      expect(screen.getByText(/Network error/)).toBeInTheDocument();
    });
  });

  it('should display empty state when no versions exist', async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ versions: [] }),
    });

    render(<VersionTimeline gameId="test-game" />);

    await waitFor(() => {
      expect(screen.getByText(/No version history available/)).toBeInTheDocument();
    });
  });

  it('should call onVersionClick when provided', async () => {
    const mockOnClick = jest.fn();
    const mockData = {
      versions: [
        {
          id: '1',
          version: 'v1',
          title: 'Version v1',
          description: '1 rule atoms',
          author: 'Test User',
          createdAt: '2024-01-01T00:00:00Z',
          changeCount: 1,
          isCurrentVersion: true,
        },
      ],
    };

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => mockData,
    });

    render(<VersionTimeline gameId="test-game" onVersionClick={mockOnClick} />);

    await waitFor(() => {
      expect(screen.getByText('Version Timeline')).toBeInTheDocument();
    });

    // Note: Cannot easily test click interaction with react-chrono without E2E
  });
});
