/**
 * LayoutShell Tests
 * Issue #5035 — LayoutShell Component
 *
 * Tests: rendering, composition (TopBar/CardRack/MiniNav/FloatingActionBar),
 * impersonation banner, CardStackPanel, NavigationProvider wrapping,
 * fullWidth prop, accessibility.
 *
 * Updated for Game Table UX redesign:
 * - TopNavbar → TopBar
 * - Sidebar → CardRack
 * - useSidebarState removed (CardRack self-manages hover state)
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

vi.mock('@/components/layout/CardRack', () => ({
  CardRack: () => (
    <nav data-testid="card-rack" aria-label="Card Rack">
      CardRack
    </nav>
  ),
}));

// Mock internal nav layers to avoid QueryClient / NavigationContext dependencies
vi.mock('@/components/layout/TopBar', () => ({
  TopBar: () => (
    <header role="banner" data-testid="top-bar">
      TopBar
    </header>
  ),
}));

vi.mock('@/components/layout/MiniNav', () => ({
  MiniNav: () => <div data-testid="mini-nav" />,
}));

vi.mock('@/components/layout/FloatingActionBar', () => ({
  FloatingActionBar: () => <div data-testid="floating-action-bar" />,
}));

vi.mock('@/components/layout/SmartFAB', () => ({
  SmartFAB: () => <div data-testid="smart-fab" />,
}));

vi.mock('@/components/layout/MobileBreadcrumb', () => ({
  MobileBreadcrumb: () => <div data-testid="mobile-breadcrumb" />,
}));

vi.mock('@/components/layout/MobileTabBar', () => ({
  MobileTabBar: () => <div data-testid="mobile-tab-bar" />,
}));

vi.mock('@/components/layout/QuickView', () => ({
  QuickView: () => <aside data-testid="quick-view" />,
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

  it('renders TopBar', () => {
    renderShell();
    expect(screen.getByTestId('top-bar')).toBeInTheDocument();
  });

  it('renders MiniNav', () => {
    renderShell();
    expect(screen.getByTestId('mini-nav')).toBeInTheDocument();
  });

  it('renders FloatingActionBar', () => {
    renderShell();
    expect(screen.getByTestId('floating-action-bar')).toBeInTheDocument();
  });

  it('renders CardRack', () => {
    renderShell();
    expect(screen.getByTestId('card-rack')).toBeInTheDocument();
  });

  it('renders content area wrapper with CardRack offset', () => {
    renderShell();
    // The outer flex wrapper carries the CardRack offset margin
    const shell = screen.getByTestId('layout-shell');
    const outerWrapper = shell.querySelector('.md\\:ml-\\[var\\(--card-rack-width\\,64px\\)\\]');
    expect(outerWrapper).toBeInTheDocument();
  });

  it('renders QuickView panel inside content area', () => {
    render(<LayoutShell>Content</LayoutShell>);
    const contentArea = screen.getByTestId('layout-content-area');
    expect(contentArea).toBeInTheDocument();
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
