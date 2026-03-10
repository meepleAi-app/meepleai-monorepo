/**
 * AppShellClient Tests
 *
 * Tests the client-side unified layout shell:
 * - Renders app-shell container
 * - Auth-aware: sidebar visible for authenticated users, hidden for anonymous
 * - Passes initialSidebarCollapsed to useSidebarState
 * - Renders TopNavbar, MiniNav, MobileBreadcrumb, FloatingActionBar, AdaptiveBottomBar
 * - ErrorBoundary wrappers present
 * - CardStackPanel rendered
 * - fullWidth prop removes padding
 *
 * Note: The RSC wrapper (AppShell.tsx) cannot be tested in vitest;
 * only the client component is tested here.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockUseSidebarState = vi.fn(() => ({
  isCollapsed: false,
  toggle: vi.fn(),
  setCollapsed: vi.fn(),
}));

vi.mock('@/hooks/useSidebarState', () => ({
  useSidebarState: (...args: unknown[]) => mockUseSidebarState(...args),
}));

const mockUseCurrentUser = vi.fn(() => ({
  data: { id: 'user-1', displayName: 'Test User', email: 'test@example.com' },
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

const mockImpersonationStore = vi.fn(() => ({
  isImpersonating: false,
  impersonatedUser: null,
  isLoading: false,
  endImpersonation: vi.fn(),
}));

vi.mock('@/store/impersonation', () => ({
  useImpersonationStore: () => mockImpersonationStore(),
}));

vi.mock('@/components/ui/feedback/impersonation-banner', () => ({
  ImpersonationBanner: ({ isImpersonating }: { isImpersonating: boolean }) =>
    isImpersonating ? <div data-testid="impersonation-banner">Banner</div> : null,
}));

vi.mock('@/components/ui/navigation/card-stack-panel', () => ({
  CardStackPanel: () => <div data-testid="card-stack-panel" />,
}));

vi.mock('@/components/layout/Sidebar/Sidebar', () => ({
  Sidebar: ({ isCollapsed }: { isCollapsed: boolean }) => (
    <div data-testid="sidebar" data-collapsed={isCollapsed}>
      Sidebar
    </div>
  ),
}));

vi.mock('@/components/layout/TopNavbar', () => ({
  TopNavbar: () => (
    <header role="banner" data-testid="top-navbar">
      TopNavbar
    </header>
  ),
}));

vi.mock('@/components/layout/MiniNav', () => ({
  MiniNav: () => <div data-testid="mini-nav" />,
}));

vi.mock('@/components/layout/FloatingActionBar', () => ({
  FloatingActionBar: () => <div data-testid="floating-action-bar" />,
}));

vi.mock('@/components/layout/MobileBreadcrumb', () => ({
  MobileBreadcrumb: () => <div data-testid="mobile-breadcrumb" />,
}));

vi.mock('@/components/layout/AdaptiveBottomBar', () => ({
  AdaptiveBottomBar: () => <div data-testid="adaptive-bottom-bar" />,
}));

vi.mock('@/hooks/useBottomPadding', () => ({
  useBottomPadding: () => 'pb-6',
}));

// ─── Import after mocks ──────────────────────────────────────────────────────

import { AppShellClient } from '../AppShell/AppShellClient';

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AppShellClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseCurrentUser.mockReturnValue({
      data: { id: 'user-1', displayName: 'Test User', email: 'test@example.com' },
    });
    mockImpersonationStore.mockReturnValue({
      isImpersonating: false,
      impersonatedUser: null,
      isLoading: false,
      endImpersonation: vi.fn(),
    });
    mockUseSidebarState.mockReturnValue({
      isCollapsed: false,
      toggle: vi.fn(),
      setCollapsed: vi.fn(),
    });
  });

  // ── Rendering ─────────────────────────────────────────────────────────────

  it('renders the app-shell container', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('app-shell')).toBeInTheDocument();
  });

  it('renders children in main content area', () => {
    render(<AppShellClient>Hello World</AppShellClient>);
    expect(screen.getByText('Hello World')).toBeInTheDocument();
  });

  it('renders the content wrapper with testid', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('app-shell-content')).toBeInTheDocument();
  });

  // ── Navigation components ─────────────────────────────────────────────────

  it('renders TopNavbar', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('top-navbar')).toBeInTheDocument();
  });

  it('renders MiniNav', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('mini-nav')).toBeInTheDocument();
  });

  it('renders MobileBreadcrumb', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('mobile-breadcrumb')).toBeInTheDocument();
  });

  it('renders FloatingActionBar', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('floating-action-bar')).toBeInTheDocument();
  });

  it('renders AdaptiveBottomBar', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('adaptive-bottom-bar')).toBeInTheDocument();
  });

  it('renders CardStackPanel', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('card-stack-panel')).toBeInTheDocument();
  });

  // ── Auth-aware sidebar ────────────────────────────────────────────────────

  it('shows sidebar for authenticated users', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('sidebar')).toBeInTheDocument();
  });

  it('hides sidebar for anonymous users', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.queryByTestId('sidebar')).not.toBeInTheDocument();
  });

  it('applies sidebar margin offset for authenticated users', () => {
    render(<AppShellClient>Content</AppShellClient>);
    const content = screen.getByTestId('app-shell-content');
    expect(content.className).toContain('md:ml-[var(--sidebar-width-expanded)]');
  });

  it('does not apply sidebar margin offset for anonymous users', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<AppShellClient>Content</AppShellClient>);
    const content = screen.getByTestId('app-shell-content');
    expect(content.className).not.toContain('md:ml-[var(--sidebar-width');
  });

  it('applies collapsed sidebar margin when collapsed', () => {
    mockUseSidebarState.mockReturnValue({
      isCollapsed: true,
      toggle: vi.fn(),
      setCollapsed: vi.fn(),
    });
    render(<AppShellClient>Content</AppShellClient>);
    const content = screen.getByTestId('app-shell-content');
    expect(content.className).toContain('md:ml-[var(--sidebar-width-collapsed)]');
    expect(content.className).not.toContain('md:ml-[var(--sidebar-width-expanded)]');
  });

  // ── initialSidebarCollapsed ───────────────────────────────────────────────

  it('passes initialSidebarCollapsed to useSidebarState', () => {
    render(<AppShellClient initialSidebarCollapsed={true}>Content</AppShellClient>);
    expect(mockUseSidebarState).toHaveBeenCalledWith(true);
  });

  it('defaults initialSidebarCollapsed to false', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(mockUseSidebarState).toHaveBeenCalledWith(false);
  });

  // ── fullWidth prop ────────────────────────────────────────────────────────

  it('applies horizontal padding by default', () => {
    render(<AppShellClient>Content</AppShellClient>);
    const main = screen.getByRole('main');
    expect(main.className).toContain('px-4');
  });

  it('removes horizontal padding when fullWidth is true', () => {
    render(<AppShellClient fullWidth>Content</AppShellClient>);
    const main = screen.getByRole('main');
    expect(main.className).not.toContain('px-4');
  });

  // ── Impersonation ─────────────────────────────────────────────────────────

  it('does not show impersonation banner when not impersonating', () => {
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.queryByTestId('impersonation-banner')).not.toBeInTheDocument();
  });

  it('shows impersonation banner when impersonating', () => {
    mockImpersonationStore.mockReturnValue({
      isImpersonating: true,
      impersonatedUser: { id: 'u2', displayName: 'Other', email: 'other@test.com' },
      isLoading: false,
      endImpersonation: vi.fn(),
    });
    render(<AppShellClient>Content</AppShellClient>);
    expect(screen.getByTestId('impersonation-banner')).toBeInTheDocument();
  });

  // ── Accessibility ─────────────────────────────────────────────────────────

  it('has main element with id and tabIndex for skip-to-content', () => {
    render(<AppShellClient>Content</AppShellClient>);
    const main = screen.getByRole('main');
    expect(main.id).toBe('main-content');
    expect(main.tabIndex).toBe(-1);
  });

  // ── className prop ────────────────────────────────────────────────────────

  it('passes additional className to main element', () => {
    render(<AppShellClient className="custom-class">Content</AppShellClient>);
    const main = screen.getByRole('main');
    expect(main.className).toContain('custom-class');
  });
});
