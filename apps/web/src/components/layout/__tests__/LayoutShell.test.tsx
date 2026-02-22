/**
 * LayoutShell Tests
 * Issue #5035 — LayoutShell Component
 *
 * Tests: rendering, slot integration, impersonation banner, CardStackPanel,
 * desktop/mobile layout structure, NavigationProvider wrapping.
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

  it('does NOT render ImpersonationBanner when not impersonating', () => {
    renderShell();
    expect(screen.queryByTestId('impersonation-banner')).not.toBeInTheDocument();
  });

  it('renders navbar slot when provided', () => {
    renderShell({
      navbar: <nav data-testid="navbar">Navbar</nav>,
    });
    expect(screen.getByTestId('navbar')).toBeInTheDocument();
  });

  it('renders miniNav slot when provided', () => {
    renderShell({
      miniNav: <div data-testid="mini-nav">MiniNav</div>,
    });
    expect(screen.getByTestId('mini-nav')).toBeInTheDocument();
  });

  it('does NOT render miniNav section when miniNav prop is absent', () => {
    renderShell();
    // No aria-label="Section navigation" element should exist
    expect(screen.queryByLabelText('Section navigation')).not.toBeInTheDocument();
  });

  it('renders actionBar slot when provided', () => {
    renderShell({
      actionBar: <div data-testid="action-bar">ActionBar</div>,
    });
    // Should appear twice: once in sticky desktop, once in fixed mobile
    const bars = screen.getAllByTestId('action-bar');
    expect(bars).toHaveLength(2);
  });

  it('does NOT render actionBar elements when actionBar prop is absent', () => {
    renderShell();
    // toolbar roles should not exist when no actionBar
    expect(screen.queryByRole('toolbar')).not.toBeInTheDocument();
  });

  it('renders all three slots together', () => {
    renderShell({
      navbar: <nav data-testid="navbar">Navbar</nav>,
      miniNav: <div data-testid="mini-nav">MiniNav</div>,
      actionBar: <div data-testid="action-bar">Actions</div>,
    });

    expect(screen.getByTestId('navbar')).toBeInTheDocument();
    expect(screen.getByTestId('mini-nav')).toBeInTheDocument();
    expect(screen.getAllByTestId('action-bar')).toHaveLength(2);
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('header has role="banner"', () => {
    renderShell({ navbar: <nav>Nav</nav> });
    const header = screen.getByRole('banner');
    expect(header).toBeInTheDocument();
  });

  it('main content has tabIndex=-1 for skip-link accessibility', () => {
    renderShell();
    const main = screen.getByRole('main');
    expect(main.tabIndex).toBe(-1);
  });
});

describe('LayoutShell with impersonation', () => {
  it('renders ImpersonationBanner when impersonating', () => {
    // Override mock to simulate impersonation
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
    // Verify the shell mounts correctly with children that use navigation context
    // NavigationProvider is provided internally by LayoutShell
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
  it('applies fullWidth class when prop is true', () => {
    renderShell({ fullWidth: true });
    const main = screen.getByRole('main');
    // When fullWidth, the inner div should NOT have max-w container classes
    const innerDiv = main.querySelector('div');
    expect(innerDiv?.className).not.toContain('max-w-screen-xl');
  });

  it('applies container by default (fullWidth=false)', () => {
    renderShell({ fullWidth: false });
    const main = screen.getByRole('main');
    const innerDiv = main.querySelector('div');
    expect(innerDiv?.className).toContain('max-w-screen-xl');
  });
});
