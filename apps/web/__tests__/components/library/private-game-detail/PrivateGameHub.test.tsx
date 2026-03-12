import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PrivateGameHub } from '@/components/library/private-game-detail/PrivateGameHub';
import { usePrivateGame } from '@/hooks/queries/useLibrary';

// Mock usePrivateGame hook
vi.mock('@/hooks/queries/useLibrary', () => ({
  usePrivateGame: vi.fn(),
}));

const mockUsePrivateGame = vi.mocked(usePrivateGame);

const mockGame = {
  id: 'game-1',
  ownerId: 'user-1',
  source: 'BoardGameGeek' as const,
  title: 'Azul',
  bggId: 230802,
  minPlayers: 2,
  maxPlayers: 4,
  yearPublished: 2017,
  thumbnailUrl: null,
  imageUrl: null,
  description: null,
  playingTimeMinutes: null,
  minAge: null,
  complexityRating: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
  bggSyncedAt: null,
};

// Mock API client
vi.mock('@/lib/api', () => ({
  api: {
    documents: { getDocumentsByGame: vi.fn().mockResolvedValue([]) },
    agents: {
      getUserAgentsForGame: vi.fn().mockResolvedValue([]),
      createUserAgent: vi.fn().mockResolvedValue({ id: 'agent-1' }),
    },
    liveSessions: {
      getActive: vi.fn().mockResolvedValue([]),
      createSession: vi.fn().mockResolvedValue('session-1'),
      resumeSession: vi.fn().mockResolvedValue(undefined),
      completeSession: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => {
    // eslint-disable-next-line jsx-a11y/alt-text
    return <img {...(props as React.ImgHTMLAttributes<HTMLImageElement>)} />;
  },
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('PrivateGameHub', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: return loaded game
    mockUsePrivateGame.mockReturnValue({
      data: mockGame,
      isLoading: false,
    } as ReturnType<typeof usePrivateGame>);
  });

  it('renders game title', () => {
    render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('renders year published', () => {
    render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText('2017')).toBeInTheDocument();
  });

  it('renders player count range', () => {
    render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    // ndash renders as an en-dash character in the DOM
    expect(screen.getByText(/2.+4 giocatori/)).toBeInTheDocument();
  });

  it('renders activation checklist heading', () => {
    render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText(/Attiva l.Assistente AI/i)).toBeInTheDocument();
  });

  it('renders Inizia Partita button', () => {
    render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByRole('button', { name: /inizia partita/i })).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading is true', () => {
    mockUsePrivateGame.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as ReturnType<typeof usePrivateGame>);

    const { container } = render(<PrivateGameHub privateGameId="game-1" />, {
      wrapper: createWrapper(),
    });
    // Skeleton uses animate-pulse or data-slot="skeleton"
    const skeletons = container.querySelectorAll(
      '[class*="animate-pulse"], [data-slot="skeleton"]'
    );
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows not-found message when game is null and not loading', () => {
    mockUsePrivateGame.mockReturnValue({
      data: undefined,
      isLoading: false,
    } as ReturnType<typeof usePrivateGame>);

    render(<PrivateGameHub privateGameId="nonexistent" />, {
      wrapper: createWrapper(),
    });
    expect(screen.getByText('Gioco non trovato.')).toBeInTheDocument();
  });
});
