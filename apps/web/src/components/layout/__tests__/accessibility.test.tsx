/**
 * Navigation Accessibility Tests
 * Issue #5057 — Accessibility + Keyboard Navigation (WCAG 2.1 AA)
 *
 * Validates ARIA roles, landmarks, and keyboard accessibility on the 3-tier
 * navigation system: TopNavbar, MiniNav, FloatingActionBar, LayoutShell.
 */

import { render, screen } from '@testing-library/react';
import { usePathname } from 'next/navigation';
import { BookOpen } from 'lucide-react';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { MiniNav } from '../MiniNav';
import { FloatingActionBar } from '../FloatingActionBar';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as Mock;

// Mock NavigationContext to inject controlled nav config
const mockNavigationState = {
  miniNav: [] as Array<{ id: string; label: string; href: string; icon?: unknown }>,
  actionBar: [] as Array<{
    id: string;
    label: string;
    icon: unknown;
    variant?: string;
    hidden?: boolean;
    disabled?: boolean;
    disabledTooltip?: string;
    onClick?: () => void;
    badge?: number | string | null;
  }>,
};

vi.mock('@/context/NavigationContext', () => ({
  NavigationProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useNavigation: () => mockNavigationState,
  useSetNavConfig: vi.fn(),
}));

// ─── Setup ───────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();
  mockUsePathname.mockReturnValue('/library');
  mockNavigationState.miniNav = [];
  mockNavigationState.actionBar = [];
});

// ─── MiniNav Accessibility ────────────────────────────────────────────────────

describe('MiniNav — ARIA landmarks and roles', () => {
  it('renders as <nav> landmark with aria-label when tabs are present', () => {
    mockNavigationState.miniNav = [
      { id: 'lib', label: 'Libreria', href: '/library', icon: BookOpen },
    ];

    render(<MiniNav />);

    const nav = screen.getByRole('navigation', { name: 'Navigazione sezione' });
    expect(nav).toBeDefined();
  });

  it('renders tablist inside nav landmark', () => {
    mockNavigationState.miniNav = [
      { id: 'lib', label: 'Libreria', href: '/library', icon: BookOpen },
    ];

    render(<MiniNav />);

    const tablist = screen.getByRole('tablist');
    expect(tablist).toBeDefined();
  });

  it('renders each tab with role="tab"', () => {
    mockNavigationState.miniNav = [
      { id: 'lib', label: 'Libreria', href: '/library' },
      { id: 'wish', label: 'Wishlist', href: '/library?tab=wishlist' },
    ];

    render(<MiniNav />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(2);
  });

  it('marks the active tab with aria-selected="true"', () => {
    mockUsePathname.mockReturnValue('/library');
    mockNavigationState.miniNav = [
      { id: 'lib', label: 'Libreria', href: '/library' },
      { id: 'wish', label: 'Wishlist', href: '/library?tab=wishlist' },
    ];

    render(<MiniNav />);

    const libraryTab = screen.getByRole('tab', { name: 'Libreria' });
    expect(libraryTab.getAttribute('aria-selected')).toBe('true');
  });

  it('marks non-active tabs with aria-selected="false"', () => {
    mockUsePathname.mockReturnValue('/library');
    mockNavigationState.miniNav = [
      { id: 'lib', label: 'Libreria', href: '/library' },
      { id: 'wish', label: 'Wishlist', href: '/library?tab=wishlist' },
    ];

    render(<MiniNav />);

    const wishlistTab = screen.getByRole('tab', { name: 'Wishlist' });
    expect(wishlistTab.getAttribute('aria-selected')).toBe('false');
  });

  it('does not render when no tabs configured', () => {
    mockNavigationState.miniNav = [];

    const { container } = render(<MiniNav />);
    expect(container.firstChild).toBeNull();
  });
});

// ─── FloatingActionBar Accessibility ─────────────────────────────────────────

describe('FloatingActionBar — ARIA toolbar and button roles', () => {
  it('renders as toolbar with aria-label', () => {
    mockNavigationState.actionBar = [
      { id: 'add', label: 'Aggiungi Gioco', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    const toolbar = screen.getByRole('toolbar', { name: 'Azioni contestuali' });
    expect(toolbar).toBeDefined();
  });

  it('renders buttons with aria-label matching action label', () => {
    mockNavigationState.actionBar = [
      { id: 'add', label: 'Aggiungi Gioco', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
      { id: 'chat', label: 'Nuova Chat', icon: BookOpen, variant: 'ghost', onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    expect(screen.getByRole('button', { name: 'Aggiungi Gioco' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'Nuova Chat' })).toBeDefined();
  });

  it('marks disabled buttons with aria-disabled', () => {
    mockNavigationState.actionBar = [
      {
        id: 'add',
        label: 'Aggiungi Gioco',
        icon: BookOpen,
        variant: 'primary',
        disabled: true,
        disabledTooltip: 'Limite raggiunto',
        onClick: vi.fn(),
      },
    ];

    render(<FloatingActionBar />);

    const button = screen.getByRole('button', { name: 'Aggiungi Gioco' });
    expect(button.getAttribute('aria-disabled')).toBe('true');
  });

  it('sets aria-describedby on disabled buttons that have a tooltip', () => {
    mockNavigationState.actionBar = [
      {
        id: 'add',
        label: 'Aggiungi Gioco',
        icon: BookOpen,
        variant: 'primary',
        disabled: true,
        disabledTooltip: 'Limite raggiunto',
        onClick: vi.fn(),
      },
    ];

    render(<FloatingActionBar />);

    const button = screen.getByRole('button', { name: 'Aggiungi Gioco' });
    expect(button.getAttribute('aria-describedby')).toBe('action-tooltip-add');
  });

  it('does not render when no actions configured', () => {
    mockNavigationState.actionBar = [];

    const { container } = render(<FloatingActionBar />);
    expect(container.firstChild).toBeNull();
  });

  it('does not render hidden actions', () => {
    mockNavigationState.actionBar = [
      { id: 'visible', label: 'Visible', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
      { id: 'hidden', label: 'Hidden', icon: BookOpen, hidden: true, onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    expect(screen.getByRole('button', { name: 'Visible' })).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Hidden' })).toBeNull();
  });
});
