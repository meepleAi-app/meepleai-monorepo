/**
 * SmartFAB Tests
 * Issue #6 from mobile-first-ux-epic.md
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { SmartFAB } from '../SmartFAB';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockPathname = vi.fn(() => '/dashboard');

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const mockUseNavigation = vi.fn();
vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => mockUseNavigation(),
}));

const mockUseScrollDirection = vi.fn();
vi.mock('@/hooks/useScrollDirection', () => ({
  useScrollDirection: () => mockUseScrollDirection(),
}));

vi.mock('@/hooks/useResponsive', () => ({
  usePrefersReducedMotion: () => false,
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

function noActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [],
    miniNavTabs: [],
  });
}

function withActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [
      { id: 'save', label: 'Save', icon: () => null, variant: 'primary', onClick: vi.fn() },
    ],
    miniNavTabs: [],
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('SmartFAB', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    noActions();
    mockUseScrollDirection.mockReturnValue(null);
    mockPathname.mockReturnValue('/dashboard');
  });

  describe('Context-aware actions', () => {
    it('shows Sparkles icon on dashboard', () => {
      mockPathname.mockReturnValue('/dashboard');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', "Chiedi all'AI");
      expect(fab).toHaveAttribute('href', '/chat/new');
    });

    it('shows Plus icon on library root', () => {
      mockPathname.mockReturnValue('/library');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Aggiungi gioco');
    });

    it('shows Play icon on library sub-page', () => {
      mockPathname.mockReturnValue('/library/some-game-id');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Avvia sessione');
    });

    it('shows Search icon on games root', () => {
      mockPathname.mockReturnValue('/games');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Cerca giochi');
    });

    it('shows Plus icon on game detail', () => {
      mockPathname.mockReturnValue('/games/abc-123');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Aggiungi alla libreria');
    });

    it('shows Plus icon on chat', () => {
      mockPathname.mockReturnValue('/chat');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Nuova chat');
    });

    it('shows Plus icon on sessions', () => {
      mockPathname.mockReturnValue('/sessions');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Nuova sessione');
    });

    it('shows Chat as default for unknown routes', () => {
      mockPathname.mockReturnValue('/unknown-route');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab).toHaveAttribute('aria-label', 'Chat');
    });
  });

  describe('Visibility', () => {
    it('hides when FloatingActionBar has visible actions', () => {
      withActions();
      const { container } = render(<SmartFAB />);
      expect(container.innerHTML).toBe('');
    });

    it('shows when no FloatingActionBar actions', () => {
      noActions();
      render(<SmartFAB />);
      expect(screen.getByTestId('smart-fab')).toBeInTheDocument();
    });

    it('hides during scroll down', () => {
      mockUseScrollDirection.mockReturnValue('down');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('opacity-0');
    });

    it('shows during scroll up', () => {
      mockUseScrollDirection.mockReturnValue('up');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).not.toContain('opacity-0');
    });
  });

  describe('Design', () => {
    it('has mobile-only class', () => {
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('md:hidden');
    });

    it('has primary color and round shape', () => {
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('bg-primary');
      expect(fab.className).toContain('rounded-full');
    });

    it('has 56px size (h-14 w-14)', () => {
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('h-14');
      expect(fab.className).toContain('w-14');
    });

    it('positions above MobileTabBar', () => {
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('bottom-[calc(72px+1rem)]');
    });
  });

  describe('Accessibility', () => {
    it('has dynamic aria-label matching context', () => {
      mockPathname.mockReturnValue('/library');
      render(<SmartFAB />);
      expect(screen.getByLabelText('Aggiungi gioco')).toBeInTheDocument();
    });

    it('has focus-visible ring', () => {
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.className).toContain('focus-visible:ring-2');
    });

    it('renders as link for navigation actions', () => {
      mockPathname.mockReturnValue('/dashboard');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.tagName).toBe('A');
      expect(fab).toHaveAttribute('href', '/chat/new');
    });

    it('renders as button for in-page actions', () => {
      mockPathname.mockReturnValue('/games');
      render(<SmartFAB />);
      const fab = screen.getByTestId('smart-fab');
      expect(fab.tagName).toBe('BUTTON');
    });
  });
});
