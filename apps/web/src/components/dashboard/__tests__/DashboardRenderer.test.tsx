import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { describe, it, expect, vi } from 'vitest';

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
// Mock OverlayHybrid — avoids its internal dependencies
// ---------------------------------------------------------------------------
vi.mock('@/components/ui/overlays', () => ({
  OverlayHybrid: () => null,
}));

// ---------------------------------------------------------------------------
// Mock new view components — thin stand-ins that expose key testids
// ---------------------------------------------------------------------------
vi.mock('../exploration/ExplorationView', () => ({
  ExplorationView: () => (
    <>
      <div data-testid="hero-zone">HeroZone</div>
      <div data-testid="tavolo-layout">TavoloLayout</div>
    </>
  ),
}));

vi.mock('../tavolo/TavoloView', () => ({
  TavoloView: () => (
    <>
      <div data-testid="session-bar">SessionBar</div>
      <div data-testid="scoreboard-zone">ScoreboardZone</div>
    </>
  ),
}));

vi.mock('../sheet/SessionSheet', () => ({
  SessionSheet: ({ isOpen, children }: { isOpen: boolean; children: React.ReactNode }) =>
    isOpen ? <div data-testid="session-sheet">{children}</div> : null,
}));

vi.mock('../sheet/SheetBreadcrumb', () => ({
  SheetBreadcrumb: () => null,
}));

vi.mock('../sheet/SheetContent', () => ({
  SheetContent: () => <div data-testid="sheet-content" />,
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
    activeSessionId: null,
    transitionTarget: null,
    activeSheet: null,
    breadcrumb: [],
    send: vi.fn(),
    openSheet: vi.fn(),
    closeSheet: vi.fn(),
    navigateCardLink: vi.fn(),
    backCardLink: vi.fn(),
  });
}

function mockGameMode() {
  mockUseDashboardMode.mockReturnValue({
    state: 'gameMode',
    isExploration: false,
    isGameMode: true,
    isTransitioning: false,
    activeSessionId: 'session-1',
    transitionTarget: null,
    activeSheet: null,
    breadcrumb: [],
    send: vi.fn(),
    openSheet: vi.fn(),
    closeSheet: vi.fn(),
    navigateCardLink: vi.fn(),
    backCardLink: vi.fn(),
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
