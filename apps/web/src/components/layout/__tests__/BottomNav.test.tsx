/**
 * Unit tests for BottomNav component
 * Issue #1829 [UI-002] BottomNav Component (Mobile-First)
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
    mockUsePathname.mockReturnValue('/dashboard');
  });

  describe('Rendering', () => {
    it('should render all 5 navigation items', () => {
      render(<BottomNav />);

      // Check all labels are present
      expect(screen.getByText('Home')).toBeInTheDocument();
      expect(screen.getByText('Giochi')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Config')).toBeInTheDocument();
      expect(screen.getByText('Profilo')).toBeInTheDocument();
    });

    it('should render correct ARIA labels', () => {
      render(<BottomNav />);

      expect(screen.getByLabelText('Navigate to dashboard home')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to games catalog')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to chat interface')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to settings')).toBeInTheDocument();
      expect(screen.getByLabelText('Navigate to user profile')).toBeInTheDocument();
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
    it('should mark /dashboard as active when pathname is /dashboard', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<BottomNav />);

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).toHaveAttribute('aria-current', 'page');
      expect(homeLink).toHaveClass('text-primary', 'font-semibold');
    });

    it('should mark /dashboard as active when pathname is / (root)', () => {
      mockUsePathname.mockReturnValue('/');
      render(<BottomNav />);

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /giochi as active for /giochi/* routes', () => {
      mockUsePathname.mockReturnValue('/giochi/catan');
      render(<BottomNav />);

      const giochiLink = screen.getByLabelText('Navigate to games catalog');
      expect(giochiLink).toHaveAttribute('aria-current', 'page');
      expect(giochiLink).toHaveClass('text-primary', 'font-semibold');
    });

    it('should mark /chat as active for /chat/* routes', () => {
      mockUsePathname.mockReturnValue('/chat/thread-123');
      render(<BottomNav />);

      const chatLink = screen.getByLabelText('Navigate to chat interface');
      expect(chatLink).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /settings as active for /settings/* routes', () => {
      mockUsePathname.mockReturnValue('/settings/profile');
      render(<BottomNav />);

      const settingsLink = screen.getByLabelText('Navigate to settings');
      expect(settingsLink).toHaveAttribute('aria-current', 'page');
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

      const homeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(homeLink).not.toHaveAttribute('aria-current');
      expect(homeLink).toHaveClass('text-muted-foreground');
      expect(homeLink).not.toHaveClass('text-primary', 'font-semibold');
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
        expect(link).toHaveClass('focus-visible:ring-2', 'focus-visible:ring-primary');
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

      expect(screen.getByLabelText('Navigate to dashboard home')).toHaveAttribute(
        'href',
        '/dashboard'
      );
      expect(screen.getByLabelText('Navigate to games catalog')).toHaveAttribute('href', '/giochi');
      expect(screen.getByLabelText('Navigate to chat interface')).toHaveAttribute('href', '/chat');
      expect(screen.getByLabelText('Navigate to settings')).toHaveAttribute('href', '/settings');
      expect(screen.getByLabelText('Navigate to user profile')).toHaveAttribute('href', '/profile');
    });

    it('should use Next.js Link component for client-side navigation', () => {
      const { container } = render(<BottomNav />);

      // Next.js Link renders as <a> with href
      const links = container.querySelectorAll('a[href^="/"]');
      expect(links.length).toBe(5);
    });
  });

  describe('Design System Compliance (Playful Boardroom)', () => {
    it('should use primary color for active state', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<BottomNav />);

      const activeLink = screen.getByLabelText('Navigate to dashboard home');
      expect(activeLink).toHaveClass('text-primary'); // Orange #F97316
    });

    it('should use muted-foreground for inactive state', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      render(<BottomNav />);

      const inactiveLink = screen.getByLabelText('Navigate to games catalog');
      expect(inactiveLink).toHaveClass('text-muted-foreground');
    });

    it('should use card background color', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('bg-card');
    });

    it('should have border-top with border color', () => {
      const { container } = render(<BottomNav />);
      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('border-t', 'border-border');
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

      const labels = screen.getAllByText(/Home|Giochi|Chat|Config|Profilo/);
      labels.forEach(label => {
        expect(label).toHaveClass('text-[10px]');
      });
    });

    it('should use Inter font for labels', () => {
      render(<BottomNav />);

      const labels = screen.getAllByText(/Home|Giochi|Chat|Config|Profilo/);
      labels.forEach(label => {
        expect(label).toHaveClass('font-[Inter]');
      });
    });
  });
});
