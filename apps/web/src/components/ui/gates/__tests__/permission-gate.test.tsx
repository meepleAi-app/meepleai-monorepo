/**
 * PermissionGate Tests
 * Epic #4068 - Issue #4178
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PermissionProvider } from '@/contexts/PermissionContext';
import { PermissionGate } from '../permission-gate';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

vi.mock('@/lib/api/permissions');

describe('PermissionGate', () => {
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

  it('renders children when permission granted', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: ['wishlist', 'bulk-select']
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <PermissionGate feature="bulk-select">
        <div>Protected Content</div>
      </PermissionGate>,
      { wrapper }
    );

    expect(await screen.findByText('Protected Content')).toBeInTheDocument();
  });

  it('hides children when permission denied', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'free',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 50, storageQuotaMB: 100 },
      accessibleFeatures: ['wishlist']
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <PermissionGate feature="bulk-select" fallback={<div>Access Denied</div>}>
        <div>Protected Content</div>
      </PermissionGate>,
      { wrapper }
    );

    expect(await screen.findByText('Access Denied')).toBeInTheDocument();
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });

  it('shows fallback when permission denied', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'free',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 50, storageQuotaMB: 100 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <PermissionGate feature="analytics.view" fallback={<div>Upgrade Required</div>}>
        <div>Analytics Dashboard</div>
      </PermissionGate>,
      { wrapper }
    );

    expect(await screen.findByText('Upgrade Required')).toBeInTheDocument();
    expect(screen.queryByText('Analytics Dashboard')).not.toBeInTheDocument();
  });
});
