/**
 * GamesFilterPanel Tests
 *
 * Verifies sidebar filter panel behavior:
 * - Quick filter links render with correct hrefs
 * - Active quick filter highlighting
 * - Advanced panel toggle (expand/collapse)
 * - Active filter count badge
 * - Apply/Reset filter button behavior
 * - Collapsed state hides text labels
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GamesFilterPanel } from '../GamesFilterPanel';

// ─── Mocks ──────────────────────────────────────────────────────────────────

const mockPush = vi.fn();
let mockPathname = '/games';
let mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => mockSearchParams,
}));

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    className,
  }: {
    href: string;
    children: React.ReactNode;
    className?: string;
  }) => (
    <a href={href} className={className}>
      {children}
    </a>
  ),
}));

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({
      children,
      initial: _initial,
      animate: _animate,
      exit: _exit,
      ...props
    }: Record<string, unknown>) => <div {...props}>{children as React.ReactNode}</div>,
  },
}));

// Mock Checkbox
vi.mock('@/components/ui/primitives/checkbox', () => ({
  Checkbox: ({
    id,
    checked,
    onCheckedChange,
    ...props
  }: {
    id: string;
    checked: boolean;
    onCheckedChange: () => void;
  }) => <input type="checkbox" id={id} checked={checked} onChange={onCheckedChange} {...props} />,
}));

vi.mock('@/components/ui/primitives/label', () => ({
  Label: ({
    children,
    ...props
  }: {
    children: React.ReactNode;
    htmlFor?: string;
    className?: string;
  }) => <label {...props}>{children}</label>,
}));

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('GamesFilterPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPathname = '/games';
    mockSearchParams = new URLSearchParams();
  });

  // ── Quick Filter Links ─────────────────────────────────────────────────

  describe('Quick Filter Links', () => {
    it('renders "Tutti i giochi" link', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('Tutti i giochi')).toBeInTheDocument();
      expect(screen.getByText('Tutti i giochi').closest('a')).toHaveAttribute('href', '/games');
    });

    it('renders "Top BGG" link with sort params', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('Top BGG')).toBeInTheDocument();
      expect(screen.getByText('Top BGG').closest('a')).toHaveAttribute(
        'href',
        '/games?sortBy=AverageRating&sortDesc=true'
      );
    });

    it('renders "2 Giocatori" link', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('2 Giocatori')).toBeInTheDocument();
      expect(screen.getByText('2 Giocatori').closest('a')).toHaveAttribute(
        'href',
        '/games?minPlayers=2&maxPlayers=2'
      );
    });

    it('renders "3-6 Giocatori" link', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('3-6 Giocatori')).toBeInTheDocument();
      expect(screen.getByText('3-6 Giocatori').closest('a')).toHaveAttribute(
        'href',
        '/games?minPlayers=3&maxPlayers=6'
      );
    });

    it('renders "Aggiunti di recente" link', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('Aggiunti di recente')).toBeInTheDocument();
      expect(screen.getByText('Aggiunti di recente').closest('a')).toHaveAttribute(
        'href',
        '/games?sortBy=CreatedAt&sortDesc=true'
      );
    });

    it('renders "Dashboard" back link', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Dashboard').closest('a')).toHaveAttribute('href', '/library');
    });
  });

  // ── Active State Highlighting ──────────────────────────────────────────

  describe('Active State', () => {
    it('highlights "Tutti i giochi" when no filters active', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      const link = screen.getByText('Tutti i giochi').closest('a');
      expect(link?.className).toContain('font-semibold');
    });

    it('highlights "Top BGG" when matching sort params are in URL', () => {
      mockSearchParams = new URLSearchParams('sortBy=AverageRating&sortDesc=true');
      render(<GamesFilterPanel isCollapsed={false} />);
      const link = screen.getByText('Top BGG').closest('a');
      expect(link?.className).toContain('font-semibold');
    });
  });

  // ── Advanced Panel Toggle ──────────────────────────────────────────────

  describe('Advanced Panel', () => {
    it('renders "Filtri avanzati" toggle button', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      expect(screen.getByText('Filtri avanzati')).toBeInTheDocument();
    });

    it('expands advanced panel on click', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      const toggle = screen.getByText('Filtri avanzati').closest('button')!;
      fireEvent.click(toggle);

      // Should show "Applica" and "Reset" buttons
      expect(screen.getByText('Applica')).toBeInTheDocument();
      expect(screen.getByText('Reset')).toBeInTheDocument();
    });

    it('shows filter selects inside advanced panel', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      const toggle = screen.getByText('Filtri avanzati').closest('button')!;
      fireEvent.click(toggle);

      // Filter labels
      expect(screen.getByText('Giocatori')).toBeInTheDocument();
      expect(screen.getByText('Tempo di gioco')).toBeInTheDocument();
      expect(screen.getByText('Difficolta')).toBeInTheDocument();
      expect(screen.getByText('Ordina per')).toBeInTheDocument();
    });

    it('has aria-expanded attribute on toggle', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      const toggle = screen.getByText('Filtri avanzati').closest('button')!;
      expect(toggle).toHaveAttribute('aria-expanded', 'false');

      fireEvent.click(toggle);
      expect(toggle).toHaveAttribute('aria-expanded', 'true');
    });
  });

  // ── Active Filter Badge ────────────────────────────────────────────────

  describe('Active Filter Badge', () => {
    it('does NOT show badge when no advanced filters active', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      // No orange badge
      expect(screen.queryByText('0')).not.toBeInTheDocument();
    });

    it('shows badge count when filters are in URL', () => {
      mockSearchParams = new URLSearchParams('categoryIds=cat1&minPlayers=2&maxPlayingTime=60');
      render(<GamesFilterPanel isCollapsed={false} />);
      // 3 active: categoryIds, players (min), playtime
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  // ── Apply / Reset ──────────────────────────────────────────────────────

  describe('Apply and Reset', () => {
    it('calls router.push on Apply click', () => {
      render(<GamesFilterPanel isCollapsed={false} />);
      // Open advanced panel
      fireEvent.click(screen.getByText('Filtri avanzati').closest('button')!);
      // Click apply
      fireEvent.click(screen.getByText('Applica'));
      expect(mockPush).toHaveBeenCalledWith('/games');
    });

    it('resets filters on Reset click', () => {
      mockSearchParams = new URLSearchParams('minPlayers=2&sortBy=Title');
      render(<GamesFilterPanel isCollapsed={false} />);
      fireEvent.click(screen.getByText('Filtri avanzati').closest('button')!);
      fireEvent.click(screen.getByText('Reset'));
      expect(mockPush).toHaveBeenCalledWith('/games');
    });

    it('preserves view param on Reset', () => {
      mockSearchParams = new URLSearchParams('view=list&minPlayers=2');
      render(<GamesFilterPanel isCollapsed={false} />);
      fireEvent.click(screen.getByText('Filtri avanzati').closest('button')!);
      fireEvent.click(screen.getByText('Reset'));
      expect(mockPush).toHaveBeenCalledWith('/games?view=list');
    });
  });

  // ── Collapsed State ────────────────────────────────────────────────────

  describe('Collapsed State', () => {
    it('hides text labels when collapsed', () => {
      render(<GamesFilterPanel isCollapsed={true} />);
      // Text labels should not render (only icons visible)
      expect(screen.queryByText('Tutti i giochi')).not.toBeInTheDocument();
      expect(screen.queryByText('Top BGG')).not.toBeInTheDocument();
    });

    it('hides advanced panel toggle when collapsed', () => {
      render(<GamesFilterPanel isCollapsed={true} />);
      expect(screen.queryByText('Filtri avanzati')).not.toBeInTheDocument();
    });
  });
});
