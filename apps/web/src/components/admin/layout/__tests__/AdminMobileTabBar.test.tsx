'use client';

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// Mock next/navigation
const mockPathname = vi.fn(() => '/admin/overview');
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));

// Mock next/link
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

import { AdminMobileTabBar } from '../AdminMobileTabBar';

describe('AdminMobileTabBar', () => {
  describe('Basic rendering', () => {
    it('renders the nav element with correct test id', () => {
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-mobile-tab-bar')).toBeInTheDocument();
    });

    it('renders with aria-label for accessibility', () => {
      render(<AdminMobileTabBar />);
      expect(screen.getByRole('navigation', { name: 'Admin navigation' })).toBeInTheDocument();
    });

    it('renders all 5 tabs', () => {
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-overview')).toBeInTheDocument();
      expect(screen.getByTestId('admin-tab-users')).toBeInTheDocument();
      expect(screen.getByTestId('admin-tab-games')).toBeInTheDocument();
      expect(screen.getByTestId('admin-tab-ai')).toBeInTheDocument();
      expect(screen.getByTestId('admin-tab-kb')).toBeInTheDocument();
    });

    it('renders tab labels', () => {
      render(<AdminMobileTabBar />);
      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('Utenti')).toBeInTheDocument();
      expect(screen.getByText('Giochi')).toBeInTheDocument();
      expect(screen.getByText('AI')).toBeInTheDocument();
      expect(screen.getByText('KB')).toBeInTheDocument();
    });
  });

  describe('Navigation links', () => {
    it('links to correct hrefs', () => {
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-overview').closest('a')).toHaveAttribute(
        'href',
        '/admin/overview'
      );
      expect(screen.getByTestId('admin-tab-users').closest('a')).toHaveAttribute(
        'href',
        '/admin/users'
      );
      expect(screen.getByTestId('admin-tab-games').closest('a')).toHaveAttribute(
        'href',
        '/admin/shared-games/all'
      );
      expect(screen.getByTestId('admin-tab-ai').closest('a')).toHaveAttribute(
        'href',
        '/admin/agents'
      );
      expect(screen.getByTestId('admin-tab-kb').closest('a')).toHaveAttribute(
        'href',
        '/admin/knowledge-base'
      );
    });
  });

  describe('Active state', () => {
    it('marks Overview as active on /admin/overview', () => {
      mockPathname.mockReturnValue('/admin/overview');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-overview')).toHaveAttribute('aria-current', 'page');
    });

    it('marks Users as active on /admin/users', () => {
      mockPathname.mockReturnValue('/admin/users');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-users')).toHaveAttribute('aria-current', 'page');
    });

    it('marks Games as active on /admin/shared-games subpaths', () => {
      mockPathname.mockReturnValue('/admin/shared-games/all');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-games')).toHaveAttribute('aria-current', 'page');
    });

    it('marks AI as active on /admin/agents subpaths', () => {
      mockPathname.mockReturnValue('/admin/agents/builder');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-ai')).toHaveAttribute('aria-current', 'page');
    });

    it('marks KB as active on /admin/knowledge-base subpaths', () => {
      mockPathname.mockReturnValue('/admin/knowledge-base/documents');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-kb')).toHaveAttribute('aria-current', 'page');
    });

    it('does not mark inactive tabs with aria-current', () => {
      mockPathname.mockReturnValue('/admin/overview');
      render(<AdminMobileTabBar />);
      expect(screen.getByTestId('admin-tab-users')).not.toHaveAttribute('aria-current');
      expect(screen.getByTestId('admin-tab-games')).not.toHaveAttribute('aria-current');
      expect(screen.getByTestId('admin-tab-ai')).not.toHaveAttribute('aria-current');
      expect(screen.getByTestId('admin-tab-kb')).not.toHaveAttribute('aria-current');
    });
  });

  describe('Mobile-only visibility', () => {
    it('has md:hidden class for mobile-only display', () => {
      render(<AdminMobileTabBar />);
      const nav = screen.getByTestId('admin-mobile-tab-bar');
      expect(nav).toHaveClass('md:hidden');
    });
  });

  describe('Layout and positioning', () => {
    it('is fixed at the bottom', () => {
      render(<AdminMobileTabBar />);
      const nav = screen.getByTestId('admin-mobile-tab-bar');
      expect(nav).toHaveClass('fixed', 'bottom-0', 'left-0', 'right-0');
    });

    it('has correct z-index', () => {
      render(<AdminMobileTabBar />);
      const nav = screen.getByTestId('admin-mobile-tab-bar');
      expect(nav).toHaveClass('z-40');
    });

    it('has glassmorphism styles', () => {
      render(<AdminMobileTabBar />);
      const nav = screen.getByTestId('admin-mobile-tab-bar');
      expect(nav).toHaveClass('backdrop-blur-md');
    });
  });

  describe('Touch targets', () => {
    it('all tabs have minimum 44x44px touch targets', () => {
      render(<AdminMobileTabBar />);
      const tabs = ['overview', 'users', 'games', 'ai', 'kb'];
      tabs.forEach(id => {
        const tab = screen.getByTestId(`admin-tab-${id}`);
        expect(tab).toHaveClass('min-w-[44px]', 'min-h-[44px]');
      });
    });
  });
});
