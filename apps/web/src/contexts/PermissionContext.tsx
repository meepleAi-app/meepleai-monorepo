'use client';

import React, { createContext, useContext, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getUserPermissions } from '@/lib/api/permissions';
import type { UserPermissions, UserTier, UserRole } from '@/types/permissions';
import { hasMinimumTier, isAdmin } from '@/types/permissions';

interface PermissionContextValue {
  tier: UserTier;
  role: UserRole;
  canAccess: (feature: string) => boolean;
  hasTier: (tier: UserTier) => boolean;
  isAdmin: () => boolean;
  loading: boolean;
}

const PermissionContext = createContext<PermissionContextValue | null>(null);

export function PermissionProvider({ children }: { children: ReactNode }) {
  const { data, isLoading } = useQuery({
    queryKey: ['permissions', 'me'],
    queryFn: getUserPermissions,
    staleTime: 5 * 60 * 1000
  });

  const value: PermissionContextValue = {
    tier: data?.tier ?? 'free',
    role: data?.role ?? 'user',
    canAccess: (feature) => data?.accessibleFeatures.includes(feature) ?? false,
    hasTier: (tier) => hasMinimumTier(data?.tier ?? 'free', tier),
    isAdmin: () => isAdmin(data?.role ?? 'user'),
    loading: isLoading
  };

  return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
}

export function usePermissions(): PermissionContextValue {
  const ctx = useContext(PermissionContext);
  if (!ctx) throw new Error('usePermissions must be within PermissionProvider');
  return ctx;
}
