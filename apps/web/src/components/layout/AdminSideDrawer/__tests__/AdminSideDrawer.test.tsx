import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminSideDrawer } from '../AdminSideDrawer';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminSideDrawer', () => {
  beforeEach(() => {
    mockUseCurrentUser.mockReset();
  });

  it('renders nothing when closed', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    const { container } = render(<AdminSideDrawer open={false} onClose={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders the four group labels for an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByText('Power-User Tools')).toBeInTheDocument();
    expect(screen.getByText('Platform & Operations')).toBeInTheDocument();
    expect(screen.getByText('AI Tooling & Data Quality')).toBeInTheDocument();
  });

  it('hides superadmin-only items from an admin (Staging Access)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.queryByRole('link', { name: /Staging Access/i })).not.toBeInTheDocument();
  });

  it('shows superadmin-only items to a superadmin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'superadmin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByRole('link', { name: /Staging Access/i })).toBeInTheDocument();
  });

  it('marks the active route with aria-current', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });
});
