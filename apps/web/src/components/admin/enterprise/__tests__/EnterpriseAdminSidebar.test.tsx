/**
 * EnterpriseAdminSidebar Tests
 * Issue #3689 - Layout Base & Navigation System
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { EnterpriseAdminSidebar } from '../EnterpriseAdminSidebar';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// Mock next/navigation
const mockPathname = vi.fn(() => '/admin/overview');
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

// Mock localStorage
const mockLocalStorage: Record<string, string> = {};
beforeEach(() => {
  Object.keys(mockLocalStorage).forEach((key) => delete mockLocalStorage[key]);
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(
    (key) => mockLocalStorage[key] ?? null
  );
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key, value) => {
    mockLocalStorage[key] = value;
  });
});

describe('EnterpriseAdminSidebar', () => {
  it('renders all 7 enterprise sections', () => {
    render(<EnterpriseAdminSidebar />);

    for (const section of ENTERPRISE_SECTIONS) {
      expect(screen.getByTestId(`enterprise-nav-${section.id}`)).toBeInTheDocument();
    }
  });

  it('renders section labels when expanded', () => {
    render(<EnterpriseAdminSidebar />);

    expect(screen.getByText('Overview')).toBeInTheDocument();
    expect(screen.getByText('Resources')).toBeInTheDocument();
    expect(screen.getByText('Operations')).toBeInTheDocument();
    expect(screen.getByText('AI Platform')).toBeInTheDocument();
    expect(screen.getByText('Users & Content')).toBeInTheDocument();
    expect(screen.getByText('Business')).toBeInTheDocument();
    expect(screen.getByText('Simulations')).toBeInTheDocument();
  });

  it('highlights the active section based on pathname', () => {
    mockPathname.mockReturnValue('/admin/resources');
    render(<EnterpriseAdminSidebar />);

    const resourcesLink = screen.getByTestId('enterprise-nav-resources');
    expect(resourcesLink).toHaveAttribute('aria-current', 'page');
  });

  it('does not highlight inactive sections', () => {
    mockPathname.mockReturnValue('/admin/overview');
    render(<EnterpriseAdminSidebar />);

    const businessLink = screen.getByTestId('enterprise-nav-business');
    expect(businessLink).not.toHaveAttribute('aria-current');
  });

  it('renders collapse/expand toggle button', () => {
    render(<EnterpriseAdminSidebar />);

    const toggle = screen.getByTestId('enterprise-sidebar-toggle');
    expect(toggle).toBeInTheDocument();
  });

  it('persists collapsed state to localStorage', async () => {
    const user = userEvent.setup();
    render(<EnterpriseAdminSidebar />);

    const toggle = screen.getByTestId('enterprise-sidebar-toggle');
    await user.click(toggle);

    expect(mockLocalStorage['enterprise-sidebar-collapsed']).toBe('true');
  });

  it('renders audit log button in footer', () => {
    render(<EnterpriseAdminSidebar />);

    const auditBtn = screen.getByTestId('enterprise-audit-log-btn');
    expect(auditBtn).toBeInTheDocument();
    expect(auditBtn).toHaveAttribute('href', '/admin/audit-log');
  });

  it('renders mobile trigger button', () => {
    render(<EnterpriseAdminSidebar />);

    const mobileTrigger = screen.getByTestId('enterprise-sidebar-mobile-trigger');
    expect(mobileTrigger).toBeInTheDocument();
  });

  it('has correct navigation aria label', () => {
    render(<EnterpriseAdminSidebar />);

    const nav = screen.getByRole('navigation', { name: 'Enterprise admin navigation' });
    expect(nav).toBeInTheDocument();
  });

  it('links to correct section URLs', () => {
    render(<EnterpriseAdminSidebar />);

    for (const section of ENTERPRISE_SECTIONS) {
      const link = screen.getByTestId(`enterprise-nav-${section.id}`);
      expect(link).toHaveAttribute('href', `/admin/${section.route}`);
    }
  });

  it('renders admin user info in footer', () => {
    render(<EnterpriseAdminSidebar />);

    expect(screen.getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('SuperAdmin')).toBeInTheDocument();
  });
});
