/**
 * Navigation Accessibility Tests
 * Issue #5057 — Accessibility + Keyboard Navigation (WCAG 2.1 AA)
 *
 * Validates ARIA roles, landmarks, and keyboard accessibility on the 3-tier
 * navigation system: TopNavbar, FloatingActionBar, LayoutShell.
 * (MiniNav removed — tabs now rendered in SidebarContextNav)
 */

import { render, screen } from '@testing-library/react';
import { BookOpen } from 'lucide-react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { FloatingActionBar } from '../FloatingActionBar';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

// Mock NavigationContext to inject controlled nav config
const mockNavigationState = {
  miniNavTabs: [] as Array<{ id: string; label: string; href: string; icon?: unknown }>,
  actionBarActions: [] as Array<{
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
  mockNavigationState.miniNavTabs = [];
  mockNavigationState.actionBarActions = [];
});

// ─── FloatingActionBar Accessibility ─────────────────────────────────────────

describe('FloatingActionBar — ARIA toolbar and button roles', () => {
  it('renders as toolbar with aria-label', () => {
    mockNavigationState.actionBarActions = [
      { id: 'add', label: 'Aggiungi Gioco', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    const toolbar = screen.getByRole('toolbar', { name: 'Azioni contestuali' });
    expect(toolbar).toBeDefined();
  });

  it('renders buttons with aria-label matching action label', () => {
    mockNavigationState.actionBarActions = [
      { id: 'add', label: 'Aggiungi Gioco', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
      { id: 'chat', label: 'Nuova Chat', icon: BookOpen, variant: 'ghost', onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    expect(screen.getByRole('button', { name: 'Aggiungi Gioco' })).toBeDefined();
    expect(screen.getByRole('button', { name: 'Nuova Chat' })).toBeDefined();
  });

  it('marks disabled buttons with aria-disabled', () => {
    mockNavigationState.actionBarActions = [
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
    mockNavigationState.actionBarActions = [
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
    mockNavigationState.actionBarActions = [];

    const { container } = render(<FloatingActionBar />);
    expect(container.firstChild).toBeNull();
  });

  it('does not render hidden actions', () => {
    mockNavigationState.actionBarActions = [
      { id: 'visible', label: 'Visible', icon: BookOpen, variant: 'primary', onClick: vi.fn() },
      { id: 'hidden', label: 'Hidden', icon: BookOpen, hidden: true, onClick: vi.fn() },
    ];

    render(<FloatingActionBar />);

    expect(screen.getByRole('button', { name: 'Visible' })).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Hidden' })).toBeNull();
  });
});
