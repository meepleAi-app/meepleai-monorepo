/**
 * Unit tests for BottomNav component
 * Issue #1829 [UI-002] BottomNav Component (Mobile-First)
 * Updated for Issue #2860 and #3104
 *
 * Coverage:
 * - Rendering (5 nav items, correct icons/labels)
 * - Active state logic (path matching)
 * - Accessibility (ARIA labels, keyboard nav, touch targets)
 * - Responsive (hidden on desktop)
 */

import { render, screen } from '@testing-library/react';
import { usePathname } from 'next/navigation';
import { vi } from 'vitest';
import { BottomNav } from '../BottomNav';

// Mock Next.js navigation (Vitest syntax)
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;

describe('BottomNav', () => {
  beforeEach(() => {
    mockUsePathname.mockReturnValue('/');
  });

  describe('Rendering', () => {
    it('should render all 5 navigation items', () => {
      render(<BottomNav />);

      // Check all labels are present (Issue #2860 updated labels)
      expect(screen.getByText('Home')).toBeInTheDocument();
      expect(screen.getByText('Libreria')).toBeInTheDocument();
      expect(screen.getByText('Catalogo')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Profilo')).toBeInTheDocument();
    });

    it('should render correct ARIA labels', () => {
      render(<BottomNav />);

      expect(screen.getByLabelText('Navigate to home page')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to your game library')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to games catalog')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to chat interface')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to your profile')).toBeInTheDocument();
    });

    it('should have primary navigation landmark', () => {
      render(<BottomNav />);
      const nav = screen.getByRole('navigation', { name: /primary mobile navigation/i });
      expect(nav).toBeInTheDocument();
    });

    it('should render as a fixed bottom nav', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('fixed', 'bottom-0', 'left-0', 'right-0');
    });

    it('should be hidden on desktop (md breakpoint)', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('md:hidden');
    });
  });

  describe('Active State Logic', () => {
    it('should mark / as active when pathname is /', () => {
      mockUsePathname.mockReturnValue('/');
      render(<BottomNav />);

      const homeLink = screen.getByLabelText('Navigate to home page');
      expect(homeLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /library as active for /library routes', () => {
      mockUsePathname.mockReturnValue('/library');
      render(<BottomNav />);

      const libraryLink = screen.getByLabelText('Navigate to your game library');
      expect(libraryLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /games as active for /games/* routes', () => {
      mockUsePathname.mockReturnValue('/games/catan');
      render(<BottomNav />);

      const catalogLink = screen.getByLabelText('Navigate to games catalog');
      expect(catalogLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /chat as active for /chat/* routes', () => {
      mockUsePathname.mockReturnValue('/chat/thread-123');
      render(<BottomNav />);

      const chatLink = screen.getByLabelText('Navigate to chat interface');
      expect(chatLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /profile as active for /profile routes', () => {
      mockUsePathname.mockReturnValue('/profile');
      render(<BottomNav />);

      const profileLink = screen.getByLabelText('Navigate to your profile');
      expect(profileLink).toHaveAttribute('aria-current', 'page');
    });

    it('should not mark multiple items as active', () => {
      mockUsePathname.mockReturnValue('/chat');
      render(<BottomNav />);

      const activeLinks = screen
        .getAllByRole('link')
        .filter(link => link.getAttribute('aria-current') === 'page');
      expect(activeLinks).toHaveLength(1);
    });

    it('should apply inactive styles to non-active links', () => {
      mockUsePathname.mockReturnValue('/chat');
      render(<BottomNav />);

      const homeLink = screen.getByLabelText('Navigate to home page');
      expect(homeLink).not.toHaveAttribute('aria-current');
      expect(homeLink).toHaveClass('text-muted-foreground');
    });
  });

  describe('Accessibility (WCAG 2.1 AA)', () => {
    it('should have minimum touch target size (44x44px)', () => {
      render(<BottomNav />);

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('min-w-[44px]', 'min-h-[44px]');
      });
    });

    it('should have keyboard focus indicators', () => {
      render(<BottomNav />);

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('focus-visible:ring-2');
      });
    });

    it('should mark icons as aria-hidden', () => {
      const { container } = render(<BottomNav />);

      // Lucide icons render as <svg> with aria-hidden
      const icons = container.querySelectorAll('svg');
      expect(icons.length).toBe(5);
      icons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });

    it('should have smooth transitions for hover/active states', () => {
      render(<BottomNav />);

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('transition-colors', 'duration-200');
      });
    });
  });

  describe('Navigation', () => {
    it('should render correct href attributes', () => {
      render(<BottomNav />);

      expect(screen.getByLabelText('Navigate to home page')).toHaveAttribute('href', '/');
      expect(screen.getByLabelText('Navigate to your game library')).toHaveAttribute(
        'href',
        '/library'
      );
      expect(screen.getByLabelText('Navigate to games catalog')).toHaveAttribute('href', '/games');
      expect(screen.getByLabelText('Navigate to chat interface')).toHaveAttribute('href', '/chat');
      expect(screen.getByLabelText('Navigate to your profile')).toHaveAttribute('href', '/profile');
    });

    it('should use Next.js Link component for client-side navigation', () => {
      const { container } = render(<BottomNav />);

      // Next.js Link renders as <a> with href
      const links = container.querySelectorAll('a[href^="/"]');
      expect(links.length).toBe(5);
    });
  });

  describe('Design System Compliance (Playful Boardroom)', () => {
    it('should use purple color for active state', () => {
      mockUsePathname.mockReturnValue('/');
      render(<BottomNav />);

      const activeLink = screen.getByLabelText('Navigate to home page');
      // Active color uses HSL purple: hsl(262 83% 62%)
      expect(activeLink).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('should use muted-foreground for inactive state', () => {
      mockUsePathname.mockReturnValue('/');
      render(<BottomNav />);

      const inactiveLink = screen.getByLabelText('Navigate to games catalog');
      expect(inactiveLink).toHaveClass('text-muted-foreground');
    });

    it('should have shadow-lg for elevation', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('shadow-lg');
    });

    it('should have z-50 for stacking context', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('z-50');
    });

    it('should use correct icon size (24x24px)', () => {
      const { container } = render(<BottomNav />);

      const icons = container.querySelectorAll('svg');
      icons.forEach(icon => {
        expect(icon).toHaveClass('w-6', 'h-6');
      });
    });

    it('should use correct label font size (10px)', () => {
      render(<BottomNav />);

      const labels = screen.getAllByText(/Home|Libreria|Catalogo|Chat|Profilo/);
      labels.forEach(label => {
        expect(label).toHaveClass('text-[10px]');
      });
    });

    it('should use Inter font for labels', () => {
      render(<BottomNav />);

      const labels = screen.getAllByText(/Home|Libreria|Catalogo|Chat|Profilo/);
      labels.forEach(label => {
        expect(label).toHaveClass('font-[Inter]');
      });
    });
  });
});
