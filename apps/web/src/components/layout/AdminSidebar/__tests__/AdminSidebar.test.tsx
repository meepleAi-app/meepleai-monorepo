import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminSidebar } from '../AdminSidebar';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminSidebar', () => {
  beforeEach(() => mockUseCurrentUser.mockReset());

  it('renders the four group labels for an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByText('AI Tooling & Data Quality')).toBeInTheDocument();
  });

  it('hides superadmin-only items from an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    expect(screen.queryByRole('link', { name: /Staging Access/i })).not.toBeInTheDocument();
  });

  it('is hidden below lg and shown at lg+ (responsive class)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    const aside = screen.getByRole('navigation', { name: /admin sidebar/i }).closest('aside');
    expect(aside).toHaveClass('hidden');
    expect(aside).toHaveClass('lg:flex');
  });

  it('renders nothing meaningful for a null user (no groups)', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<AdminSidebar />);
    expect(screen.queryByText('Admin Console')).not.toBeInTheDocument();
  });
});
