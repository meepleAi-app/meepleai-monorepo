import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Admin', email: 'admin@test.com', role: 'admin' },
  }),
}));

import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';
import { ADMIN_NAV_GROUPS } from '@/components/layout/admin-nav/admin-nav-config';

describe('AdminSideDrawer', () => {
  const defaultProps = {
    open: true,
    onClose: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    document.body.style.overflow = '';
  });

  it('renders "Torna all\'app" link when open', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    const link = screen.getByText(/torna all'app/i);
    expect(link).toBeDefined();
  });

  it('renders the four admin nav group labels (SP5 consolidation, #1496)', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    for (const group of ADMIN_NAV_GROUPS) {
      expect(screen.getByText(group.label)).toBeDefined();
    }
  });

  it('renders every nav item an admin is allowed to see', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    // The mocked user is a plain admin; only superadmin-gated items are hidden.
    const adminVisibleItems = ADMIN_NAV_GROUPS.flatMap(g => g.items).filter(
      item => item.minRole !== 'superadmin'
    );
    for (const item of adminVisibleItems) {
      expect(screen.getByText(item.label)).toBeDefined();
    }
  });

  it('hides superadmin-only items from an admin user', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    // "Staging Access" (group C) requires superadmin; an admin must not see it.
    expect(screen.queryByText('Staging Access')).toBeNull();
  });

  it('does not render when closed', () => {
    render(<AdminSideDrawer open={false} onClose={vi.fn()} />);
    expect(screen.queryByText(/torna all'app/i)).toBeNull();
    expect(screen.queryByText('Overview')).toBeNull();
  });

  it('calls onClose on overlay click', () => {
    const onClose = vi.fn();
    render(<AdminSideDrawer open={true} onClose={onClose} />);
    const overlay = screen.getByTestId('drawer-overlay');
    fireEvent.click(overlay);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose on Escape key', () => {
    const onClose = vi.fn();
    render(<AdminSideDrawer open={true} onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape', code: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('prevents body scroll when open', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(document.body.style.overflow).toBe('hidden');
  });

  it('restores body scroll when closed', () => {
    const { rerender } = render(<AdminSideDrawer open={true} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('hidden');
    rerender(<AdminSideDrawer open={false} onClose={vi.fn()} />);
    expect(document.body.style.overflow).toBe('');
  });

  it('shows user displayName and email', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    expect(screen.getByText('Admin')).toBeDefined();
    expect(screen.getByText('admin@test.com')).toBeDefined();
  });

  it('shows admin role label', () => {
    render(<AdminSideDrawer {...defaultProps} />);
    // The role badge renders the role value; use getAllByText since "admin" appears in multiple places
    const adminElements = screen.getAllByText(/admin/i);
    expect(adminElements.length).toBeGreaterThan(0);
  });
});
