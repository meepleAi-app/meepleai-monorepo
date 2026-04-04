/**
 * Centralized role utilities.
 *
 * The backend (Role.cs) normalizes all roles to lowercase via ToLowerInvariant().
 * The frontend MUST treat lowercase as canonical and normalize any external input.
 *
 * Usage sites: AuthModal redirect, proxy.ts middleware, RequireRole component,
 *              InlineRoleSelect, admin users page, user-role-badge.
 */

/** Canonical lowercase role type matching backend Role.cs */
export type CanonicalRole = 'user' | 'editor' | 'creator' | 'admin' | 'superadmin';

/** All valid roles in lowercase */
export const VALID_ROLES: CanonicalRole[] = ['user', 'editor', 'creator', 'admin', 'superadmin'];

/** Roles assignable by admins (excludes superadmin and creator) */
export const ASSIGNABLE_ROLES: CanonicalRole[] = ['user', 'editor', 'admin'];

/** Display names for UI rendering (PascalCase) */
const ROLE_DISPLAY_NAMES: Record<CanonicalRole, string> = {
  user: 'User',
  editor: 'Editor',
  creator: 'Creator',
  admin: 'Admin',
  superadmin: 'SuperAdmin',
};

/**
 * Normalize any role string to canonical lowercase.
 * Handles PascalCase, UPPERCASE, mixed case from any source.
 */
export function normalizeRole(role: string | null | undefined): CanonicalRole {
  if (!role) return 'user';
  const lower = role.toLowerCase() as CanonicalRole;
  return VALID_ROLES.includes(lower) ? lower : 'user';
}

/**
 * Get display-friendly PascalCase name for a role.
 */
export function displayRole(role: string | null | undefined): string {
  return ROLE_DISPLAY_NAMES[normalizeRole(role)];
}

/**
 * Check if role is admin or superadmin (case-insensitive).
 */
export function isAdminRole(role: string | null | undefined): boolean {
  if (!role) return false;
  const normalized = normalizeRole(role);
  return normalized === 'admin' || normalized === 'superadmin';
}
