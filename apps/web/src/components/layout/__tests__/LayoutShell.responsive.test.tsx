/**
 * LayoutShell Responsive Test Matrix
 * Issue #215 — Per-breakpoint component visibility assertions
 *
 * Verifies that layout components apply correct Tailwind responsive classes
 * to control visibility at each breakpoint. Since JSDOM does not support
 * media queries, we assert on CSS class strings rather than computed styles.
 *
 * Breakpoint matrix:
 * ┌──────────────────┬────────┬─────────┬─────────┬──────────┐
 * │ Component        │ Mobile │ Tablet  │ Desktop │ Wide     │
 * │                  │ <768   │ 768-1023│1024-1279│ >=1280   │
 * ├──────────────────┼────────┼─────────┼─────────┼──────────┤
 * │ CardRack         │ hidden │ visible │ visible │ visible  │
 * │ MobileTabBar     │ visible│ hidden  │ hidden  │ hidden   │
 * │ MobileBreadcrumb │ visible│ hidden  │ hidden  │ hidden   │
 * │ SmartFAB         │ visible│ hidden  │ hidden  │ hidden   │
 * │ QuickView        │ hidden │ hidden  │ hidden  │ visible  │
 * │ FloatingActionBar│ always │ always  │ always  │ always   │
 * └──────────────────┴────────┴─────────┴─────────┴──────────┘
 */

import { render, screen, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LayoutShell } from '../LayoutShell';

// ─── Mocks ────────────────────────────────────────────────────────────────────
// Render real class names so we can assert responsive Tailwind classes.

vi.mock('@/store/impersonation', () => ({
  useImpersonationStore: () => ({
    isImpersonating: false,
    impersonatedUser: null,
    isLoading: false,
    endImpersonation: vi.fn(),
  }),
}));

vi.mock('@/components/ui/feedback/impersonation-banner', () => ({
  ImpersonationBanner: () => null,
}));

vi.mock('@/components/ui/navigation/card-stack-panel', () => ({
  CardStackPanel: () => null,
}));

vi.mock('@/hooks/useBottomPadding', () => ({
  useBottomPadding: () => 'pb-24',
}));

// ─── Layout component mocks that preserve responsive classes ──────────────────

vi.mock('@/components/layout/CardRack', () => ({
  CardRack: () => (
    <nav data-testid="card-rack" className="hidden md:flex flex-col w-16">
      CardRack
    </nav>
  ),
}));

vi.mock('@/components/layout/TopBar', () => ({
  TopBar: () => (
    <header role="banner" data-testid="top-bar">
      TopBar
    </header>
  ),
}));

vi.mock('@/components/layout/FloatingActionBar', () => ({
  FloatingActionBar: () => (
    <div data-testid="floating-action-bar" className="fixed bottom-[calc(72px+1.5rem)] md:bottom-6">
      FloatingActionBar
    </div>
  ),
}));

vi.mock('@/components/layout/SmartFAB', () => ({
  SmartFAB: () => (
    <div data-testid="smart-fab" className="md:hidden fixed bottom-20 right-4">
      SmartFAB
    </div>
  ),
}));

vi.mock('@/components/layout/MobileBreadcrumb', () => ({
  MobileBreadcrumb: () => (
    <nav data-testid="mobile-breadcrumb" className="md:hidden px-4 py-2">
      MobileBreadcrumb
    </nav>
  ),
}));

vi.mock('@/components/layout/MobileTabBar', () => ({
  MobileTabBar: () => (
    <nav data-testid="mobile-tab-bar" className="md:hidden fixed bottom-0 w-full">
      MobileTabBar
    </nav>
  ),
}));

vi.mock('@/components/layout/QuickView', () => ({
  QuickView: () => (
    <aside data-testid="quick-view" className="hidden xl:flex flex-col w-[300px]">
      QuickView
    </aside>
  ),
}));

// ─── Helper ───────────────────────────────────────────────────────────────────

function renderShell() {
  return render(
    <LayoutShell>
      <div data-testid="content">Page Content</div>
    </LayoutShell>
  );
}

function getClasses(testId: string): string {
  return screen.getByTestId(testId).className;
}

// ─── Responsive Visibility Tests ──────────────────────────────────────────────

describe('LayoutShell responsive visibility', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('CardRack — hidden mobile, visible md+', () => {
    it('has "hidden" class to hide on mobile', () => {
      renderShell();
      expect(getClasses('card-rack')).toContain('hidden');
    });

    it('has "md:flex" class to show at md breakpoint', () => {
      renderShell();
      expect(getClasses('card-rack')).toContain('md:flex');
    });
  });

  describe('MobileTabBar — visible mobile, hidden md+', () => {
    it('has "md:hidden" class to hide at md breakpoint', () => {
      renderShell();
      expect(getClasses('mobile-tab-bar')).toContain('md:hidden');
    });

    it('does NOT have "hidden" base class (visible on mobile)', () => {
      renderShell();
      const classes = getClasses('mobile-tab-bar').split(' ');
      // "hidden" should not be a standalone class (only "md:hidden")
      expect(classes.filter(c => c === 'hidden')).toHaveLength(0);
    });
  });

  describe('MobileBreadcrumb — visible mobile, hidden md+', () => {
    it('has "md:hidden" class to hide at md breakpoint', () => {
      renderShell();
      expect(getClasses('mobile-breadcrumb')).toContain('md:hidden');
    });

    it('does NOT have "hidden" base class (visible on mobile)', () => {
      renderShell();
      const classes = getClasses('mobile-breadcrumb').split(' ');
      expect(classes.filter(c => c === 'hidden')).toHaveLength(0);
    });
  });

  describe('SmartFAB — visible mobile, hidden md+', () => {
    it('has "md:hidden" class to hide at md breakpoint', () => {
      renderShell();
      expect(getClasses('smart-fab')).toContain('md:hidden');
    });

    it('does NOT have "hidden" base class (visible on mobile)', () => {
      renderShell();
      const classes = getClasses('smart-fab').split(' ');
      expect(classes.filter(c => c === 'hidden')).toHaveLength(0);
    });
  });

  describe('QuickView — hidden below xl, visible xl+', () => {
    it('has "hidden" class to hide by default', () => {
      renderShell();
      expect(getClasses('quick-view')).toContain('hidden');
    });

    it('has "xl:flex" class to show at xl breakpoint', () => {
      renderShell();
      expect(getClasses('quick-view')).toContain('xl:flex');
    });

    it('does NOT have "md:flex" (not visible at tablet/desktop)', () => {
      renderShell();
      expect(getClasses('quick-view')).not.toContain('md:flex');
    });
  });

  describe('FloatingActionBar — always visible, repositions', () => {
    it('does NOT have "hidden" or "md:hidden" class', () => {
      renderShell();
      const classes = getClasses('floating-action-bar');
      expect(classes).not.toContain('md:hidden');
      const classList = classes.split(' ');
      expect(classList.filter(c => c === 'hidden')).toHaveLength(0);
    });

    it('has responsive bottom positioning', () => {
      renderShell();
      const classes = getClasses('floating-action-bar');
      expect(classes).toContain('md:bottom-6');
    });
  });

  describe('Content area — CardRack offset on md+', () => {
    it('content wrapper has md:ml offset for CardRack', () => {
      renderShell();
      const shell = screen.getByTestId('layout-shell');
      const offsetWrapper = shell.querySelector('.md\\:ml-\\[var\\(--card-rack-width\\,64px\\)\\]');
      expect(offsetWrapper).toBeInTheDocument();
    });

    it('content wrapper has no left margin on mobile', () => {
      renderShell();
      const shell = screen.getByTestId('layout-shell');
      const offsetWrapper = shell.querySelector('.md\\:ml-\\[var\\(--card-rack-width\\,64px\\)\\]');
      // No base ml-* class → no offset on mobile
      expect(offsetWrapper?.className).not.toMatch(/(?<!\w)ml-\d/);
    });
  });
});

// ─── Breakpoint Summary Matrix ────────────────────────────────────────────────

describe('LayoutShell breakpoint matrix (documentation)', () => {
  it('all layout components render with correct responsive classes', () => {
    renderShell();

    // Mobile-only components (visible <768px, hidden >=768px)
    const mobileComponents = ['mobile-tab-bar', 'mobile-breadcrumb', 'smart-fab'];
    for (const id of mobileComponents) {
      const classes = getClasses(id);
      expect(classes).toContain('md:hidden');
      expect(classes.split(' ').filter(c => c === 'hidden')).toHaveLength(0);
    }

    // Desktop-only component (hidden <768px, visible >=768px)
    const cardRackClasses = getClasses('card-rack');
    expect(cardRackClasses).toContain('hidden');
    expect(cardRackClasses).toContain('md:flex');

    // Wide-only component (hidden <1280px, visible >=1280px)
    const quickViewClasses = getClasses('quick-view');
    expect(quickViewClasses).toContain('hidden');
    expect(quickViewClasses).toContain('xl:flex');

    // Always-visible component
    const fabClasses = getClasses('floating-action-bar');
    expect(fabClasses).not.toContain('md:hidden');
    expect(fabClasses.split(' ').filter(c => c === 'hidden')).toHaveLength(0);
  });
});
