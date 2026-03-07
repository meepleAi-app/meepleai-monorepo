/**
 * LayoutShell Tests
 * Issue #5035 — LayoutShell Component
 *
 * Tests: rendering, composition (TopNavbar/MiniNav/FloatingActionBar),
 * impersonation banner, CardStackPanel, NavigationProvider wrapping,
 * fullWidth prop, accessibility.
 *
 * Updated: slot-based API replaced with self-contained composition
 * (TopNavbar/MiniNav/FloatingActionBar are rendered internally, not via props).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LayoutShell } from '../LayoutShell';
import type { LayoutShellProps } from '../LayoutShell';

// ─── Mocks ────────────────────────────────────────────────────────────────────

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

vi.mock('@/hooks/useSidebarState', () => ({
  useSidebarState: () => ({ isCollapsed: false, toggle: vi.fn(), setCollapsed: vi.fn() }),
}));

// Mock internal nav layers to avoid QueryClient / NavigationContext dependencies
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

vi.mock('@/components/layout/MobileTabBar', () => ({
  MobileTabBar: () => <div data-testid="mobile-tab-bar" />,
}));

vi.mock('@/hooks/useBottomPadding', () => ({
  useBottomPadding: () => 'pb-24',
}));

// ─── Helper ───────────────────────────────────────────────────────────────────

function renderShell(props: Partial<LayoutShellProps> = {}) {
  return render(
    <LayoutShell {...props}>
      <div data-testid="content">Page Content</div>
    </LayoutShell>
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('LayoutShell', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders children', () => {
    renderShell();
    expect(screen.getByTestId('content')).toBeInTheDocument();
    expect(screen.getByText('Page Content')).toBeInTheDocument();
  });

  it('renders the outer shell container', () => {
    renderShell();
    expect(screen.getByTestId('layout-shell')).toBeInTheDocument();
  });

  it('renders CardStackPanel', () => {
    renderShell();
    expect(screen.getByTestId('card-stack-panel')).toBeInTheDocument();
  });

  it('renders main content with id="main-content"', () => {
    renderShell();
    const main = screen.getByRole('main');
    expect(main.id).toBe('main-content');
  });

  it('renders TopNavbar', () => {
    renderShell();
    expect(screen.getByTestId('top-navbar')).toBeInTheDocument();
  });

  it('renders MiniNav', () => {
    renderShell();
    expect(screen.getByTestId('mini-nav')).toBeInTheDocument();
  });

  it('renders FloatingActionBar', () => {
    renderShell();
    expect(screen.getByTestId('floating-action-bar')).toBeInTheDocument();
  });

  it('renders Sidebar', () => {
    renderShell();
    expect(screen.getByTestId('sidebar')).toBeInTheDocument();
  });

  it('renders content area wrapper with sidebar offset', () => {
    renderShell();
    expect(screen.getByTestId('layout-content-area')).toBeInTheDocument();
  });

  it('does NOT render ImpersonationBanner when not impersonating', () => {
    renderShell();
    expect(screen.queryByTestId('impersonation-banner')).not.toBeInTheDocument();
  });

  it('header has role="banner"', () => {
    renderShell();
    expect(screen.getByRole('banner')).toBeInTheDocument();
  });

  it('main content has tabIndex=-1 for skip-link accessibility', () => {
    renderShell();
    const main = screen.getByRole('main');
    expect(main.tabIndex).toBe(-1);
  });
});

describe('LayoutShell with impersonation', () => {
  it('renders ImpersonationBanner when impersonating', () => {
    mockImpersonationStore.mockReturnValueOnce({
      isImpersonating: true,
      impersonatedUser: { id: '1', displayName: 'Test User', email: 'test@test.com' },
      isLoading: false,
      endImpersonation: vi.fn(),
    });

    render(
      <LayoutShell>
        <div>Content</div>
      </LayoutShell>
    );

    expect(screen.getByTestId('impersonation-banner')).toBeInTheDocument();
  });
});

describe('LayoutShell NavigationProvider integration', () => {
  it('wraps children with NavigationProvider (consumer renders without error)', () => {
    let rendered = false;

    const TestChild = () => {
      rendered = true;
      return <div data-testid="nav-consumer">OK</div>;
    };

    render(
      <LayoutShell>
        <TestChild />
      </LayoutShell>
    );

    expect(screen.getByTestId('nav-consumer')).toBeInTheDocument();
    expect(rendered).toBe(true);
  });
});

describe('LayoutShell fullWidth prop', () => {
  it('removes horizontal padding when fullWidth=true', () => {
    renderShell({ fullWidth: true });
    const main = screen.getByRole('main');
    expect(main.className).not.toContain('px-4');
  });

  it('applies horizontal padding by default (fullWidth=false)', () => {
    renderShell({ fullWidth: false });
    const main = screen.getByRole('main');
    expect(main.className).toContain('px-4');
  });
});
