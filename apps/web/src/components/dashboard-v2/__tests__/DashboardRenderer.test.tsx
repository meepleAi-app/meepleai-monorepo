import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { describe, it, expect, vi, type Mock } from 'vitest';

import { DashboardRenderer } from '../DashboardRenderer';

// ---------------------------------------------------------------------------
// Mock useDashboardMode — avoids needing the full XState provider
// ---------------------------------------------------------------------------
const mockUseDashboardMode = vi.fn();

vi.mock('../useDashboardMode', () => ({
  useDashboardMode: (...args: unknown[]) => mockUseDashboardMode(...args),
}));

// ---------------------------------------------------------------------------
// Mock useDashboardSearchStore — avoids zustand store dependency
// ---------------------------------------------------------------------------
vi.mock('@/stores/useDashboardSearchStore', () => ({
  useDashboardSearchStore: () => ({
    selectedGame: null,
    setSelectedGame: vi.fn(),
    openChatDrawer: vi.fn(),
    drawerState: null,
    closeChatDrawer: vi.fn(),
  }),
}));

// ---------------------------------------------------------------------------
// Mock AddToLibraryModal — avoids query client dependency
// ---------------------------------------------------------------------------
vi.mock('../AddToLibraryModal', () => ({
  AddToLibraryModal: () => null,
}));

// ---------------------------------------------------------------------------
// Mock tavolo components — avoids their internal dependencies
// ---------------------------------------------------------------------------
vi.mock('../tavolo', () => ({
  TavoloLayout: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="tavolo-layout">{children}</div>
  ),
  TavoloSection: ({ children, title }: { children: React.ReactNode; title: string }) => (
    <div data-testid={`tavolo-section-${title}`}>{children}</div>
  ),
  ActiveSessionCard: () => <div data-testid="active-session-card" />,
}));

// ---------------------------------------------------------------------------
// Mock zone components — avoids their internal dependencies
// ---------------------------------------------------------------------------
vi.mock('../zones', () => ({
  HeroZone: () => <div data-testid="hero-zone">HeroZone</div>,
  SessionBar: () => <div data-testid="session-bar">SessionBar</div>,
  ScoreboardZone: () => <div data-testid="scoreboard-zone">ScoreboardZone</div>,
}));

// ---------------------------------------------------------------------------
// Mock ExtraMeepleCardDrawer — avoids heavy component dependency
// ---------------------------------------------------------------------------
vi.mock('@/components/ui/data-display/extra-meeple-card', () => ({
  ExtraMeepleCardDrawer: () => null,
}));

// ---------------------------------------------------------------------------
// Mock next/navigation — avoids router invariant error
// ---------------------------------------------------------------------------
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
}));

// ---------------------------------------------------------------------------
// Mock data hooks — avoids API calls
// ---------------------------------------------------------------------------
vi.mock('@/hooks/queries/useActiveSessions', () => ({
  useActiveSessions: () => ({ data: { sessions: [] }, isLoading: false }),
}));

vi.mock('@/hooks/queries/useAgents', () => ({
  useAgents: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useRecentlyAddedGames: () => ({ data: { items: [] }, isLoading: false }),
}));

// ---------------------------------------------------------------------------
// Mock framer-motion — lightweight passthrough to avoid animation issues
// ---------------------------------------------------------------------------
vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({
      children,
      ...props
    }: React.HTMLAttributes<HTMLDivElement> & {
      layoutId?: string;
      initial?: unknown;
      animate?: unknown;
      exit?: unknown;
      transition?: unknown;
    }) => {
      // Strip framer-motion-specific props so React doesn't warn
      const { layoutId, initial, animate, exit, transition, ...htmlProps } = props as Record<
        string,
        unknown
      >;
      return <div {...(htmlProps as React.HTMLAttributes<HTMLDivElement>)}>{children}</div>;
    },
  },
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function mockExploration() {
  mockUseDashboardMode.mockReturnValue({
    state: 'exploration',
    isExploration: true,
    isGameMode: false,
    isTransitioning: false,
    isExpanded: false,
    activeSessionId: null,
    transitionTarget: null,
    send: vi.fn(),
  });
}

function mockGameMode() {
  mockUseDashboardMode.mockReturnValue({
    state: 'gameMode',
    isExploration: false,
    isGameMode: true,
    isTransitioning: false,
    isExpanded: false,
    activeSessionId: 'session-1',
    transitionTarget: null,
    send: vi.fn(),
  });
}

// ---------------------------------------------------------------------------
// Render helper with QueryClientProvider
// ---------------------------------------------------------------------------
function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
describe('DashboardRenderer', () => {
  it('renders the dashboard-renderer root element', () => {
    mockExploration();
    renderWithProviders(<DashboardRenderer />);
    expect(screen.getByTestId('dashboard-renderer')).toBeInTheDocument();
  });

  it('shows hero zone and tavolo layout when state is exploration', () => {
    mockExploration();
    renderWithProviders(<DashboardRenderer />);

    expect(screen.getByTestId('hero-zone')).toBeInTheDocument();
    expect(screen.getByTestId('tavolo-layout')).toBeInTheDocument();
  });

  it('shows game mode zones when state is gameMode', () => {
    mockGameMode();
    renderWithProviders(<DashboardRenderer />);

    expect(screen.getByTestId('session-bar')).toBeInTheDocument();
    expect(screen.getByTestId('scoreboard-zone')).toBeInTheDocument();
  });

  it('does not show hero zone in gameMode', () => {
    mockGameMode();
    renderWithProviders(<DashboardRenderer />);

    expect(screen.queryByTestId('hero-zone')).not.toBeInTheDocument();
    expect(screen.queryByTestId('tavolo-layout')).not.toBeInTheDocument();
  });

  it('does not show game mode zones in exploration', () => {
    mockExploration();
    renderWithProviders(<DashboardRenderer />);

    expect(screen.queryByTestId('session-bar')).not.toBeInTheDocument();
    expect(screen.queryByTestId('scoreboard-zone')).not.toBeInTheDocument();
  });
});
