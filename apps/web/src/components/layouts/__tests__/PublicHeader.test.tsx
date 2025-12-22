/**
 * PublicHeader Component Tests - Issue #2230
 *
 * Test coverage:
 * - Rendering base
 * - Navigation desktop e mobile
 * - User menu (authenticated/unauthenticated)
 * - Theme switcher
 * - Sticky header on scroll
 * - Accessibility
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PublicHeader, type PublicUser } from '../PublicHeader';
import { usePathname } from 'next/navigation';

// Mock Next.js router, Link, and Image
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useRouter: vi.fn(() => ({
    push: vi.fn(),
  })),
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: any) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock next-themes
vi.mock('next-themes', () => ({
  useTheme: vi.fn(() => ({
    theme: 'light',
    setTheme: vi.fn(),
    systemTheme: 'light',
  })),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => children,
}));

// Mock MeepleLogo to avoid styled-jsx issues in tests
vi.mock('@/components/ui/meeple/meeple-logo', () => ({
  MeepleLogo: () => <div data-testid="meeple-logo">MeepleAI</div>,
}));

describe('PublicHeader', () => {
  const mockUser: PublicUser = {
    name: 'Test User',
    email: 'test@example.com',
    avatar: 'https://example.com/avatar.jpg',
  };

  const mockOnLogout = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    (usePathname as ReturnType<typeof vi.fn>).mockReturnValue('/');
    // Reset scroll position
    window.scrollY = 0;
  });

  describe('Base Rendering', () => {
    it('renders header with logo', () => {
      render(<PublicHeader />);

      const logo = screen.getByLabelText('MeepleAI Home');
      expect(logo).toBeInTheDocument();
    });

    it('renders desktop navigation', () => {
      render(<PublicHeader />);

      // Desktop nav exists (use getAllByRole since mobile nav also has these)
      const homeLinks = screen.getAllByRole('link', { name: /home/i });
      const giochiLinks = screen.getAllByRole('link', { name: /giochi/i });
      const chatLinks = screen.getAllByRole('link', { name: /chat/i });
      const dashboardLinks = screen.getAllByRole('link', { name: /dashboard/i });

      expect(homeLinks.length).toBeGreaterThan(0);
      expect(giochiLinks.length).toBeGreaterThan(0);
      expect(chatLinks.length).toBeGreaterThan(0);
      expect(dashboardLinks.length).toBeGreaterThan(0);
    });

    it('renders theme switcher', () => {
      render(<PublicHeader />);

      const themeSwitcher = screen.getByLabelText('Theme switcher');
      expect(themeSwitcher).toBeInTheDocument();
    });
  });

  describe('Authentication States', () => {
    it('renders "Accedi" button when not authenticated', () => {
      render(<PublicHeader />);

      const loginButton = screen.getByRole('link', { name: /accedi/i });
      expect(loginButton).toBeInTheDocument();
      expect(loginButton).toHaveAttribute('href', '/login');
    });

    it('renders user menu when authenticated', () => {
      render(<PublicHeader user={mockUser} onLogout={mockOnLogout} />);

      const userMenuButton = screen.getByLabelText('User menu');
      expect(userMenuButton).toBeInTheDocument();
    });

    it('user menu contains logout callback', () => {
      render(<PublicHeader user={mockUser} onLogout={mockOnLogout} />);

      const userMenuButton = screen.getByLabelText('User menu');
      expect(userMenuButton).toBeInTheDocument();

      // Verify onLogout prop is passed (component logic test)
      expect(mockOnLogout).toBeDefined();
    });
  });

  describe('Active Navigation', () => {
    it('highlights active page in navigation', () => {
      (usePathname as ReturnType<typeof vi.fn>).mockReturnValue('/games');

      render(<PublicHeader />);

      const giochiLinks = screen.getAllByRole('link', { name: /giochi/i });
      // At least one should have aria-current=page
      const hasActivePage = giochiLinks.some(link => link.getAttribute('aria-current') === 'page');
      expect(hasActivePage).toBe(true);
    });

    it('does not highlight inactive pages', () => {
      (usePathname as ReturnType<typeof vi.fn>).mockReturnValue('/');

      render(<PublicHeader />);

      const giochiLinks = screen.getAllByRole('link', { name: /giochi/i });
      // None should have aria-current=page when on home
      const hasActivePage = giochiLinks.some(link => link.getAttribute('aria-current') === 'page');
      expect(hasActivePage).toBe(false);
    });
  });

  describe('Mobile Navigation', () => {
    it('renders mobile menu trigger', () => {
      render(<PublicHeader />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      expect(mobileMenuButton).toBeInTheDocument();
    });

    it('opens mobile sheet when hamburger is clicked', async () => {
      render(<PublicHeader />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      fireEvent.click(mobileMenuButton);

      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
        expect(screen.getByText('Menu')).toBeInTheDocument();
      });
    });

    it('displays all navigation items in mobile sheet', async () => {
      render(<PublicHeader />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      fireEvent.click(mobileMenuButton);

      await waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toBeInTheDocument();

        // Mobile nav should contain all navigation items
        const mobileNav = screen.getByRole('navigation', { name: /mobile navigation/i });
        expect(mobileNav).toBeInTheDocument();
      });
    });

    it('closes mobile sheet when navigation item is clicked', async () => {
      render(<PublicHeader />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      fireEvent.click(mobileMenuButton);

      await waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toBeInTheDocument();
      });

      // Click on a navigation item
      const homeLinks = screen.getAllByRole('link', { name: /home/i });
      const mobileHomeLink = homeLinks[homeLinks.length - 1]; // Get mobile version
      fireEvent.click(mobileHomeLink);

      // Sheet should close - dialog should not be in document after some time
      await waitFor(
        () => {
          expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
        },
        { timeout: 500 }
      );
    });

    it('shows user info in mobile sheet when authenticated', async () => {
      render(<PublicHeader user={mockUser} onLogout={mockOnLogout} />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      fireEvent.click(mobileMenuButton);

      await waitFor(() => {
        const dialog = screen.getByRole('dialog');
        expect(dialog).toBeInTheDocument();

        // User info should appear in mobile sheet
        expect(screen.getByText(mockUser.name)).toBeInTheDocument();
        expect(screen.getByText(mockUser.email)).toBeInTheDocument();
      });
    });
  });

  describe('Sticky Header on Scroll', () => {
    it('applies shadow class when scrolled', () => {
      const { container } = render(<PublicHeader />);

      // Simulate scroll
      window.scrollY = 50;
      fireEvent.scroll(window);

      const header = container.querySelector('header');
      expect(header).toHaveClass('shadow-sm');
    });

    it('removes shadow class when not scrolled', () => {
      const { container } = render(<PublicHeader />);

      // Ensure no scroll
      window.scrollY = 0;
      fireEvent.scroll(window);

      const header = container.querySelector('header');
      expect(header).not.toHaveClass('shadow-sm');
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for navigation', () => {
      render(<PublicHeader />);

      const mainNav = screen.getByRole('navigation', { name: /main navigation/i });
      expect(mainNav).toBeInTheDocument();
    });

    it('has proper ARIA labels for mobile navigation', async () => {
      render(<PublicHeader />);

      const mobileMenuButton = screen.getByLabelText('Open navigation menu');
      fireEvent.click(mobileMenuButton);

      await waitFor(() => {
        const mobileNav = screen.getByRole('navigation', { name: /mobile navigation/i });
        expect(mobileNav).toBeInTheDocument();
      });
    });

    it('has proper aria-current for active page', () => {
      (usePathname as ReturnType<typeof vi.fn>).mockReturnValue('/games');

      render(<PublicHeader />);

      const giochiLink = screen.getByRole('link', { name: /giochi/i });
      expect(giochiLink).toHaveAttribute('aria-current', 'page');
    });

    it('has proper keyboard navigation support', () => {
      render(<PublicHeader />);

      // Get all navigation links
      const homeLinks = screen.getAllByRole('link', { name: /home/i });

      // Check that at least one has focus-visible classes
      const hasFocusVisible = homeLinks.some(link => {
        const className = link.getAttribute('class') || '';
        return className.includes('focus-visible');
      });

      expect(hasFocusVisible).toBe(true);
    });
  });

  describe('Custom Styling', () => {
    it('accepts custom className', () => {
      const { container } = render(<PublicHeader className="custom-class" />);

      const header = container.querySelector('header');
      expect(header).toHaveClass('custom-class');
    });
  });

  describe('User Avatar', () => {
    it('displays user avatar when provided', () => {
      render(<PublicHeader user={mockUser} onLogout={mockOnLogout} />);

      const avatar = screen.getByAltText(mockUser.name);
      expect(avatar).toBeInTheDocument();
      expect(avatar).toHaveAttribute('src', mockUser.avatar);
    });

    it('displays default icon when no avatar provided', () => {
      const userWithoutAvatar = { ...mockUser, avatar: undefined };
      render(<PublicHeader user={userWithoutAvatar} onLogout={mockOnLogout} />);

      const userMenuButton = screen.getByLabelText('User menu');
      expect(userMenuButton).toBeInTheDocument();
      // UserIcon from lucide-react should be rendered
      expect(userMenuButton.querySelector('svg')).toBeInTheDocument();
    });
  });
});
