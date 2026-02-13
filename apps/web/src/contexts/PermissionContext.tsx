'use client';

import React, { createContext, useContext, type ReactNode } from 'react';

import { useQuery } from '@tanstack/react-query';

import { getUserPermissions } from '@/lib/api/permissions';
import type { UserTier, UserRole } from '@/types/permissions';
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
  const { data, isLoading, error } = useQuery({
    queryKey: ['permissions', 'me'],
    queryFn: getUserPermissions,
    staleTime: 5 * 60 * 1000,
    retry: 2
  });

  // Error handling: fallback to safe default permissions
  if (error) {
    console.error('Failed to load user permissions:', error);

    // Return minimal safe defaults (Free tier, User role)
    const safeDefaults: PermissionContextValue = {
      tier: 'free',
      role: 'user',
      canAccess: () => false, // Deny all features on error (safe default)
      hasTier: () => false,
      isAdmin: () => false,
      loading: false
    };

    return <PermissionContext.Provider value={safeDefaults}>{children}</PermissionContext.Provider>;
  }

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
