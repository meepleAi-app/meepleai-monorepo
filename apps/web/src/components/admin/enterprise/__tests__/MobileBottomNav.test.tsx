/**
 * MobileBottomNav Tests
 * Issue #3689 - Layout Base & Navigation System
 */

import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

import { MobileBottomNav } from '../MobileBottomNav';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

const mockPathname = vi.fn(() => '/admin/overview');

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));

describe('MobileBottomNav', () => {
  it('renders all 7 section links', () => {
    render(<MobileBottomNav />);

    for (const section of ENTERPRISE_SECTIONS) {
      expect(screen.getByTestId(`enterprise-mobile-nav-${section.id}`)).toBeInTheDocument();
    }
  });

  it('renders correct hrefs for each section', () => {
    render(<MobileBottomNav />);

    for (const section of ENTERPRISE_SECTIONS) {
      const link = screen.getByTestId(`enterprise-mobile-nav-${section.id}`);
      expect(link).toHaveAttribute('href', `/admin/${section.route}`);
    }
  });

  it('highlights the active section', () => {
    mockPathname.mockReturnValue('/admin/overview');
    render(<MobileBottomNav />);

    const overviewLink = screen.getByTestId('enterprise-mobile-nav-overview');
    expect(overviewLink).toHaveAttribute('aria-current', 'page');
  });

  it('does not highlight inactive sections', () => {
    mockPathname.mockReturnValue('/admin/overview');
    render(<MobileBottomNav />);

    const businessLink = screen.getByTestId('enterprise-mobile-nav-business');
    expect(businessLink).not.toHaveAttribute('aria-current');
  });

  it('renders section labels', () => {
    render(<MobileBottomNav />);

    expect(screen.getByText('Overview')).toBeInTheDocument();
    expect(screen.getByText('Resources')).toBeInTheDocument();
    expect(screen.getByText('Operations')).toBeInTheDocument();
    expect(screen.getByText('AI Platform')).toBeInTheDocument();
    expect(screen.getByText('Business')).toBeInTheDocument();
    expect(screen.getByText('Simulations')).toBeInTheDocument();
  });

  it('has correct navigation aria label', () => {
    render(<MobileBottomNav />);

    const nav = screen.getByRole('navigation', { name: 'Enterprise mobile navigation' });
    expect(nav).toBeInTheDocument();
  });
});
