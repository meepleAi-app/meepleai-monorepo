/**
 * RoleGate Tests
 * Epic #4068 - Issue #4178
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PermissionProvider } from '@/contexts/PermissionContext';
import { RoleGate } from '../role-gate';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

vi.mock('@/lib/api/permissions');

describe('RoleGate', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>{children}</PermissionProvider>
    </QueryClientProvider>
  );

  it('renders children when role matches', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'normal',
      role: 'admin',
      status: 'Active',
      limits: { maxGames: 100, storageQuotaMB: 500 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <RoleGate role="admin">
        <div>Admin Panel</div>
      </RoleGate>,
      { wrapper }
    );

    expect(await screen.findByText('Admin Panel')).toBeInTheDocument();
  });

  it('hides children when role does not match', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <RoleGate role="admin" fallback={<div>Role Denied</div>}>
        <div>Admin Panel</div>
      </RoleGate>,
      { wrapper }
    );

    expect(await screen.findByText('Role Denied')).toBeInTheDocument();
    expect(screen.queryByText('Admin Panel')).not.toBeInTheDocument();
  });

  it('supports multiple roles (array)', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'normal',
      role: 'creator',
      status: 'Active',
      limits: { maxGames: 100, storageQuotaMB: 500 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <RoleGate role={['admin', 'creator', 'editor']}>
        <div>Content Management</div>
      </RoleGate>,
      { wrapper }
    );

    expect(await screen.findByText('Content Management')).toBeInTheDocument();
  });

  it('shows fallback when role insufficient', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'enterprise',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <RoleGate role="admin" fallback={<div>Admin Access Required</div>}>
        <div>Admin Settings</div>
      </RoleGate>,
      { wrapper }
    );

    expect(await screen.findByText('Admin Access Required')).toBeInTheDocument();
    expect(screen.queryByText('Admin Settings')).not.toBeInTheDocument();
  });
});
