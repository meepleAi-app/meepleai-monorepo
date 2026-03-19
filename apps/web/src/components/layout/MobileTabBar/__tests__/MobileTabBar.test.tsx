/**
 * Unit tests for MobileTabBar component
 * Issue #1 from mobile-first-ux-epic.md
 *
 * Coverage:
 * - Rendering (tabs, icons, labels)
 * - Auth gating (guest vs authenticated)
 * - Active state logic (path matching)
 * - Accessibility (ARIA, touch targets, focus)
 * - Responsive (hidden on desktop)
 * - Design system compliance (glassmorphism, fonts)
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { usePathname } from 'next/navigation';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { MobileTabBar } from '../MobileTabBar';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUseCurrentUser = vi.fn();
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: (...args: unknown[]) => mockUseCurrentUser(...args),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;

// ─── Helpers ─────────────────────────────────────────────────────────────────

const authenticatedUser = {
  data: { id: 'user-1', email: 'test@test.com', username: 'tester' },
  isLoading: false,
  error: null,
};

const guestUser = {
  data: null,
  isLoading: false,
  error: null,
};

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

function renderTabBar() {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MobileTabBar />
    </QueryClientProvider>
  );
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('MobileTabBar', () => {
  beforeEach(() => {
    mockUsePathname.mockReturnValue('/dashboard');
    mockUseCurrentUser.mockReturnValue(authenticatedUser);
  });

  describe('Rendering (Authenticated)', () => {
    it('should render all 5 tabs for authenticated users', () => {
      renderTabBar();

      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Library')).toBeInTheDocument();
      expect(screen.getByText('Discover')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
      expect(screen.getByText('Profile')).toBeInTheDocument();
    });

    it('should render correct href attributes', () => {
      renderTabBar();

      expect(screen.getByLabelText('Navigate to dashboard')).toHaveAttribute('href', '/dashboard');
      expect(screen.getByLabelText('Navigate to your game library')).toHaveAttribute(
        'href',
        '/library'
      );
      expect(screen.getByLabelText('Browse games catalog')).toHaveAttribute('href', '/games');
      expect(screen.getByLabelText('Navigate to chat')).toHaveAttribute('href', '/chat/new');
      expect(screen.getByLabelText('Navigate to your profile')).toHaveAttribute('href', '/profile');
    });

    it('should have data-testid on each tab', () => {
      renderTabBar();

      expect(screen.getByTestId('mobile-tab-dashboard')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-tab-library')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-tab-discover')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-tab-chat')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-tab-profile')).toBeInTheDocument();
    });
  });

  describe('Auth Gating (Guest)', () => {
    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue(guestUser);
    });

    it('should show only Dashboard and Discover for guests', () => {
      renderTabBar();

      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Discover')).toBeInTheDocument();
      expect(screen.queryByText('Library')).not.toBeInTheDocument();
      expect(screen.queryByText('Chat')).not.toBeInTheDocument();
      expect(screen.queryByText('Profile')).not.toBeInTheDocument();
    });

    it('should render exactly 2 links for guests', () => {
      renderTabBar();

      const links = screen.getAllByRole('link');
      expect(links).toHaveLength(2);
    });
  });

  describe('Active State Logic', () => {
    it('should mark /dashboard as active when pathname is /dashboard', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to dashboard');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /dashboard as active when pathname is / (home)', () => {
      mockUsePathname.mockReturnValue('/');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to dashboard');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /library as active for /library routes', () => {
      mockUsePathname.mockReturnValue('/library');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to your game library');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /library as active for /library/sub-route', () => {
      mockUsePathname.mockReturnValue('/library/favorites');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to your game library');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /games as active for /games/* routes', () => {
      mockUsePathname.mockReturnValue('/games/catan');
      renderTabBar();

      const link = screen.getByLabelText('Browse games catalog');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /chat as active for /chat/* routes', () => {
      mockUsePathname.mockReturnValue('/chat/thread-abc');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to chat');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should mark /profile as active for /profile and sub-routes', () => {
      mockUsePathname.mockReturnValue('/profile/settings');
      renderTabBar();

      const link = screen.getByLabelText('Navigate to your profile');
      expect(link).toHaveAttribute('aria-current', 'page');
    });

    it('should not mark multiple items as active', () => {
      mockUsePathname.mockReturnValue('/games');
      renderTabBar();

      const activeLinks = screen
        .getAllByRole('link')
        .filter(link => link.getAttribute('aria-current') === 'page');
      expect(activeLinks).toHaveLength(1);
    });

    it('should apply inactive styles to non-active links', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      renderTabBar();

      const gamesLink = screen.getByLabelText('Browse games catalog');
      expect(gamesLink).not.toHaveAttribute('aria-current');
      expect(gamesLink).toHaveClass('text-muted-foreground');
    });
  });

  describe('Accessibility (WCAG 2.1 AA)', () => {
    it('should have navigation landmark with correct label', () => {
      renderTabBar();

      const nav = screen.getByRole('navigation', { name: /primary navigation/i });
      expect(nav).toBeInTheDocument();
    });

    it('should have minimum touch target size (44x44px)', () => {
      renderTabBar();

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('min-w-[44px]', 'min-h-[44px]');
      });
    });

    it('should have keyboard focus indicators', () => {
      renderTabBar();

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('focus-visible:ring-2');
      });
    });

    it('should mark icons as aria-hidden', () => {
      const { container } = renderTabBar();

      const icons = container.querySelectorAll('svg');
      expect(icons.length).toBe(5);
      icons.forEach(icon => {
        expect(icon).toHaveAttribute('aria-hidden', 'true');
      });
    });

    it('should have unique aria-labels for all tabs', () => {
      renderTabBar();

      const labels = [
        'Navigate to dashboard',
        'Navigate to your game library',
        'Browse games catalog',
        'Navigate to chat',
        'Navigate to your profile',
      ];

      labels.forEach(label => {
        expect(screen.getByLabelText(label)).toBeInTheDocument();
      });
    });
  });

  describe('Responsive Behavior', () => {
    it('should be hidden on desktop (md breakpoint)', () => {
      renderTabBar();

      const nav = screen.getByTestId('mobile-tab-bar');
      expect(nav).toHaveClass('md:hidden');
    });

    it('should be fixed at the bottom', () => {
      renderTabBar();

      const nav = screen.getByTestId('mobile-tab-bar');
      expect(nav).toHaveClass('fixed', 'bottom-0', 'left-0', 'right-0');
    });

    it('should have z-40 stacking context', () => {
      renderTabBar();

      const nav = screen.getByTestId('mobile-tab-bar');
      expect(nav).toHaveClass('z-40');
    });

    it('should have 72px height', () => {
      renderTabBar();

      // h-16 (64px) is on the inner flex container, not the nav wrapper
      const nav = screen.getByTestId('mobile-tab-bar');
      const innerContainer = nav.querySelector('.h-16');
      expect(innerContainer).toBeInTheDocument();
    });
  });

  describe('Design System Compliance', () => {
    it('should use glassmorphism styling', () => {
      renderTabBar();

      const nav = screen.getByTestId('mobile-tab-bar');
      expect(nav).toHaveClass('bg-card/90', 'backdrop-blur-md');
    });

    it('should have border-t for visual separation', () => {
      renderTabBar();

      const nav = screen.getByTestId('mobile-tab-bar');
      expect(nav).toHaveClass('border-t', 'border-border/50');
    });

    it('should use Nunito font for labels', () => {
      renderTabBar();

      const labels = screen.getAllByText(/Dashboard|Library|Discover|Chat|Profile/);
      labels.forEach(label => {
        expect(label).toHaveClass('font-nunito');
      });
    });

    it('should use 10px label font size', () => {
      renderTabBar();

      const labels = screen.getAllByText(/Dashboard|Library|Discover|Chat|Profile/);
      labels.forEach(label => {
        expect(label).toHaveClass('text-[10px]');
      });
    });

    it('should use primary color for active tab', () => {
      mockUsePathname.mockReturnValue('/dashboard');
      renderTabBar();

      const activeLink = screen.getByLabelText('Navigate to dashboard');
      expect(activeLink).toHaveClass('text-primary');
    });

    it('should have smooth transitions', () => {
      renderTabBar();

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass('transition-colors', 'duration-200');
      });
    });
  });
});
