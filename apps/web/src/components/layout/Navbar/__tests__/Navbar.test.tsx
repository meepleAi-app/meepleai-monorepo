/**
 * Navbar Tests
 * Issue #3288 - Phase 2: Navbar Components
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';

import { LayoutProvider } from '../../LayoutProvider';
import { HamburgerButton } from '../HamburgerButton';
import { NavItems, type NavItem } from '../NavItems';
import { Home, Settings, User } from 'lucide-react';

// Create mockable pathname
let mockPathname = '/';

// Mock dependencies
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(() => ({
    data: null,
    isLoading: false,
  })),
}));

vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'desktop',
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    viewportWidth: 1024,
  }),
}));

// Helper wrapper
function TestWrapper({ children }: { children: ReactNode }) {
  return <LayoutProvider>{children}</LayoutProvider>;
}

describe('HamburgerButton', () => {
  it('should render with hamburger icon when closed', () => {
    const onToggle = vi.fn();
    render(<HamburgerButton isOpen={false} onToggle={onToggle} />);

    const button = screen.getByRole('button', { name: /apri menu/i });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });

  it('should render with X icon when open', () => {
    const onToggle = vi.fn();
    render(<HamburgerButton isOpen={true} onToggle={onToggle} />);

    const button = screen.getByRole('button', { name: /chiudi menu/i });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it('should call onToggle when clicked', () => {
    const onToggle = vi.fn();
    render(<HamburgerButton isOpen={false} onToggle={onToggle} />);

    const button = screen.getByRole('button');
    fireEvent.click(button);

    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('should have accessible touch target', () => {
    const onToggle = vi.fn();
    render(<HamburgerButton isOpen={false} onToggle={onToggle} />);

    const button = screen.getByRole('button');
    // Check that it has the touch target classes
    expect(button).toHaveClass('h-11', 'w-11');
  });
});

describe('NavItems', () => {
  const mockItems: NavItem[] = [
    {
      href: '/',
      label: 'Home',
      icon: Home,
      ariaLabel: 'Navigate to home',
      testId: 'nav-home',
    },
    {
      href: '/settings',
      label: 'Settings',
      icon: Settings,
      ariaLabel: 'Navigate to settings',
      testId: 'nav-settings',
    },
    {
      href: '/profile',
      label: 'Profile',
      icon: User,
      ariaLabel: 'Navigate to profile',
      testId: 'nav-profile',
    },
  ];

  it('should render all navigation items', () => {
    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    expect(screen.getByTestId('nav-home')).toBeInTheDocument();
    expect(screen.getByTestId('nav-settings')).toBeInTheDocument();
    expect(screen.getByTestId('nav-profile')).toBeInTheDocument();
  });

  it('should render items with icons', () => {
    render(<NavItems items={mockItems} showIcons={true} />, { wrapper: TestWrapper });

    // Icons are rendered with aria-hidden
    const icons = document.querySelectorAll('[aria-hidden="true"]');
    expect(icons.length).toBeGreaterThanOrEqual(3);
  });

  it('should render items with labels', () => {
    render(<NavItems items={mockItems} showLabels={true} />, { wrapper: TestWrapper });

    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
    expect(screen.getByText('Profile')).toBeInTheDocument();
  });

  it('should hide labels when showLabels is false', () => {
    render(<NavItems items={mockItems} showLabels={false} />, { wrapper: TestWrapper });

    expect(screen.queryByText('Home')).not.toBeInTheDocument();
    expect(screen.queryByText('Settings')).not.toBeInTheDocument();
  });

  it('should mark active item with aria-current', () => {
    mockPathname = '/';

    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    const homeLink = screen.getByTestId('nav-home');
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('should render horizontally by default', () => {
    render(<NavItems items={mockItems} direction="horizontal" />, { wrapper: TestWrapper });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveClass('flex-row');
  });

  it('should render vertically when specified', () => {
    render(<NavItems items={mockItems} direction="vertical" />, { wrapper: TestWrapper });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveClass('flex-col');
  });

  it('should call onItemClick when item is clicked', () => {
    const onItemClick = vi.fn();
    render(<NavItems items={mockItems} onItemClick={onItemClick} />, { wrapper: TestWrapper });

    const homeLink = screen.getByTestId('nav-home');
    fireEvent.click(homeLink);

    expect(onItemClick).toHaveBeenCalledWith(expect.objectContaining({ href: '/' }));
  });
});

describe('NavItems active state', () => {
  const mockItems: NavItem[] = [
    { href: '/', label: 'Home', icon: Home },
    { href: '/dashboard', label: 'Dashboard', icon: Settings },
    { href: '/profile', label: 'Profile', icon: User },
  ];

  it('should mark home as active for root path', () => {
    mockPathname = '/';

    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    const links = screen.getAllByRole('link');
    const homeLink = links.find(link => link.getAttribute('href') === '/');
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('should mark dashboard as active for exact path', () => {
    mockPathname = '/dashboard';

    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    const links = screen.getAllByRole('link');
    const dashLink = links.find(link => link.getAttribute('href') === '/dashboard');
    expect(dashLink).toHaveAttribute('aria-current', 'page');
  });

  it('should mark profile as active for nested paths', () => {
    mockPathname = '/profile/settings';

    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    const links = screen.getAllByRole('link');
    const profileLink = links.find(link => link.getAttribute('href') === '/profile');
    expect(profileLink).toHaveAttribute('aria-current', 'page');
  });
});

describe('Navbar accessibility', () => {
  it('should have proper aria labels', () => {
    render(<HamburgerButton isOpen={false} onToggle={() => {}} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label');
    expect(button).toHaveAttribute('aria-expanded');
    expect(button).toHaveAttribute('aria-controls');
  });

  it('should have navigation landmark', () => {
    const mockItems: NavItem[] = [
      { href: '/', label: 'Home', icon: Home },
    ];

    render(<NavItems items={mockItems} />, { wrapper: TestWrapper });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveAttribute('aria-label', 'Main navigation');
  });
});
