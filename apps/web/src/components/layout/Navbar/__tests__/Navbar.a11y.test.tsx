/**
 * Accessibility Tests for Navbar Components (Issue #2929)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 *
 * Coverage:
 * - HamburgerButton accessibility
 * - NavItems navigation landmark
 * - Keyboard navigation
 * - Focus management
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';
import { Home, Settings, User } from 'lucide-react';

import { LayoutProvider } from '../../LayoutProvider';
import { HamburgerButton } from '../HamburgerButton';
import { NavItems, type NavItem } from '../NavItems';

// Mock dependencies
vi.mock('next/navigation', () => ({
  usePathname: () => '/',
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

// Test data
const mockNavItems: NavItem[] = [
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

// Wrapper component
function TestWrapper({ children }: { children: ReactNode }) {
  return <LayoutProvider>{children}</LayoutProvider>;
}

describe('HamburgerButton - Accessibility', () => {
  it('should have no accessibility violations (closed state)', async () => {
    // Include the referenced mobile-menu element for aria-controls validity
    const { container } = render(
      <>
        <HamburgerButton isOpen={false} onToggle={() => {}} />
        <div id="mobile-menu" aria-hidden="true" />
      </>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (open state)', async () => {
    // Include the referenced mobile-menu element for aria-controls validity
    const { container } = render(
      <>
        <HamburgerButton isOpen={true} onToggle={() => {}} />
        <div id="mobile-menu" aria-hidden="false" />
      </>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have aria-expanded attribute', () => {
    render(<HamburgerButton isOpen={false} onToggle={() => {}} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });

  it('should have aria-controls attribute', () => {
    render(<HamburgerButton isOpen={false} onToggle={() => {}} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-controls');
  });

  it('should have descriptive aria-label', () => {
    render(<HamburgerButton isOpen={false} onToggle={() => {}} />);

    const button = screen.getByRole('button');
    expect(button).toHaveAttribute('aria-label');
    expect(button.getAttribute('aria-label')).toBeTruthy();
  });

  it('should change aria-label based on state', () => {
    const { rerender } = render(
      <HamburgerButton isOpen={false} onToggle={() => {}} />
    );

    let button = screen.getByRole('button');
    const closedLabel = button.getAttribute('aria-label');

    rerender(<HamburgerButton isOpen={true} onToggle={() => {}} />);

    button = screen.getByRole('button');
    const openLabel = button.getAttribute('aria-label');

    // Labels should be different for different states
    expect(closedLabel).not.toBe(openLabel);
  });
});

describe('NavItems - Accessibility', () => {
  it('should have no accessibility violations', async () => {
    const { container } = render(
      <NavItems items={mockNavItems} />,
      { wrapper: TestWrapper }
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (vertical direction)', async () => {
    const { container } = render(
      <NavItems items={mockNavItems} direction="vertical" />,
      { wrapper: TestWrapper }
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations (icons only)', async () => {
    const { container } = render(
      <NavItems items={mockNavItems} showLabels={false} showIcons={true} />,
      { wrapper: TestWrapper }
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have navigation landmark', () => {
    render(<NavItems items={mockNavItems} />, { wrapper: TestWrapper });

    const nav = screen.getByRole('navigation');
    expect(nav).toBeInTheDocument();
  });

  it('should have aria-label on navigation', () => {
    render(<NavItems items={mockNavItems} />, { wrapper: TestWrapper });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveAttribute('aria-label', 'Main navigation');
  });

  it('should mark current page with aria-current', () => {
    render(<NavItems items={mockNavItems} />, { wrapper: TestWrapper });

    const homeLink = screen.getByTestId('nav-home');
    expect(homeLink).toHaveAttribute('aria-current', 'page');
  });

  it('should have accessible names for all links', () => {
    render(<NavItems items={mockNavItems} />, { wrapper: TestWrapper });

    const links = screen.getAllByRole('link');
    links.forEach(link => {
      // Links should have accessible name via text content or aria-label
      expect(
        link.textContent?.trim() || link.getAttribute('aria-label')
      ).toBeTruthy();
    });
  });

  it('should hide decorative icons from screen readers', () => {
    render(
      <NavItems items={mockNavItems} showIcons={true} />,
      { wrapper: TestWrapper }
    );

    // Icons should have aria-hidden="true"
    const icons = document.querySelectorAll('svg');
    icons.forEach(icon => {
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    });
  });
});

describe('Keyboard Navigation - Accessibility', () => {
  it('should allow focus on navigation links', () => {
    render(<NavItems items={mockNavItems} />, { wrapper: TestWrapper });

    const links = screen.getAllByRole('link');
    links.forEach(link => {
      expect(link).toBeVisible();
      // Links are naturally focusable, shouldn't have negative tabindex
      expect(link).not.toHaveAttribute('tabindex', '-1');
    });
  });
});
