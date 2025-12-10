/**
 * AdminAuthGuard Component Tests - Issue #887
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AdminAuthGuard } from '../AdminAuthGuard';
import type { AuthUser } from '@/types/auth';

const adminUser: AuthUser = {
  id: 'user-1',
  email: 'admin@example.com',
  role: 'Admin',
  displayName: 'Admin User',
};

describe('AdminAuthGuard', () => {
  it('renders loading state with default message', () => {
    render(
      <AdminAuthGuard loading user={null}>
        <div>secret content</div>
      </AdminAuthGuard>
    );

    expect(screen.getByText('Authenticating...')).toBeInTheDocument();
    expect(screen.queryByText('secret content')).not.toBeInTheDocument();
  });

  it('renders loading state with custom message and background class', () => {
    const { container } = render(
      <AdminAuthGuard
        loading
        user={null}
        loadingMessage="Checking admin session..."
        backgroundClass="bg-test-surface"
      >
        <div>hidden content</div>
      </AdminAuthGuard>
    );

    expect(screen.getByText('Checking admin session...')).toBeInTheDocument();
    expect(container.firstChild).toHaveClass('bg-test-surface');
  });

  it('blocks access when user is missing', () => {
    render(
      <AdminAuthGuard loading={false} user={null}>
        <div>should not render</div>
      </AdminAuthGuard>
    );

    expect(screen.getByText('Unauthorized Access')).toBeInTheDocument();
    const loginLink = screen.getByRole('link', { name: /go to login/i });
    expect(loginLink).toHaveAttribute('href', '/login');
    expect(screen.queryByText('should not render')).not.toBeInTheDocument();
  });

  it('blocks access when user is not admin', () => {
    const viewerUser: AuthUser = { ...adminUser, role: 'User' };

    render(
      <AdminAuthGuard loading={false} user={viewerUser}>
        <div>viewer content</div>
      </AdminAuthGuard>
    );

    expect(screen.getByText('Unauthorized Access')).toBeInTheDocument();
    expect(screen.queryByText('viewer content')).not.toBeInTheDocument();
  });

  it('renders children for admin users', () => {
    render(
      <AdminAuthGuard loading={false} user={adminUser}>
        <p>admin content</p>
      </AdminAuthGuard>
    );

    expect(screen.getByText('admin content')).toBeInTheDocument();
    expect(screen.queryByText('Unauthorized Access')).not.toBeInTheDocument();
  });
});
