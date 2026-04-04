/**
 * Permission system types (Epic #4068 - Issue #4177)
 */

export type UserTier = 'free' | 'normal' | 'premium' | 'pro' | 'enterprise';
export type UserRole = 'user' | 'editor' | 'creator' | 'admin' | 'superadmin';
export type UserAccountStatus = 'Active' | 'Suspended' | 'Banned' | 'Pending';

export interface CollectionLimits {
  maxGames: number;
  storageQuotaMB: number;
}

export interface UserPermissions {
  tier: UserTier;
  role: UserRole;
  status: UserAccountStatus;
  limits: CollectionLimits;
  accessibleFeatures: string[];
}

export interface PermissionCheckResponse {
  hasAccess: boolean;
  reason: string;
  details: {
    userTier: string;
    userRole: string;
    userStatus: string;
    required: {
      tier?: string;
      role?: string;
      states?: string[];
    };
    logic: string;
  };
}

export const TIER_HIERARCHY: Record<UserTier, number> = {
  free: 0,
  normal: 1,
  premium: 2,
  pro: 2,
  enterprise: 3,
};

export function hasMinimumTier(userTier: UserTier, requiredTier: UserTier): boolean {
  return TIER_HIERARCHY[userTier] >= TIER_HIERARCHY[requiredTier];
}

export const ROLE_HIERARCHY: Record<UserRole, number> = {
  user: 0,
  creator: 1,
  editor: 2,
  admin: 3,
  superadmin: 4,
};

export function hasMinimumRole(userRole: UserRole, requiredRole: UserRole): boolean {
  return ROLE_HIERARCHY[userRole] >= ROLE_HIERARCHY[requiredRole];
}

export function isAdmin(role: UserRole): boolean {
  return hasMinimumRole(role, 'admin');
}

export function isSuperAdmin(role: UserRole): boolean {
  return role === 'superadmin';
}

export function isEditor(role: UserRole): boolean {
  return role === 'editor' || role === 'admin' || role === 'superadmin';
}

export function isCreator(role: UserRole): boolean {
  return role === 'creator' || isEditor(role);
}
