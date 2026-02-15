/**
 * TierGate Tests
 * Epic #4068 - Issue #4178
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PermissionProvider } from '@/contexts/PermissionContext';
import { TierGate } from '../tier-gate';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

vi.mock('@/lib/api/permissions');

describe('TierGate', () => {
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

  it('renders children when tier sufficient (Pro >= Normal)', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <TierGate tier="normal">
        <div>Normal Tier Content</div>
      </TierGate>,
      { wrapper }
    );

    expect(await screen.findByText('Normal Tier Content')).toBeInTheDocument();
  });

  it('hides children when tier insufficient (Free < Pro)', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'free',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 50, storageQuotaMB: 100 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <TierGate tier="pro" fallback={<div>Tier Denied</div>}>
        <div>Pro Tier Content</div>
      </TierGate>,
      { wrapper }
    );

    expect(await screen.findByText('Tier Denied')).toBeInTheDocument();
    expect(screen.queryByText('Pro Tier Content')).not.toBeInTheDocument();
  });

  it('shows fallback when tier insufficient', async () => {
    const mockPermissions: UserPermissions = {
      tier: 'normal',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 100, storageQuotaMB: 500 },
      accessibleFeatures: []
    };

    vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

    render(
      <TierGate tier="enterprise" fallback={<div>Enterprise Only</div>}>
        <div>Enterprise Features</div>
      </TierGate>,
      { wrapper }
    );

    expect(await screen.findByText('Enterprise Only')).toBeInTheDocument();
    expect(screen.queryByText('Enterprise Features')).not.toBeInTheDocument();
  });
});
