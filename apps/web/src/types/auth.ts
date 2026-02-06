/**
 * Authentication & Session Management Types
 * Centralized type definitions for authentication, authorization, and session management
 */

/**
 * Authenticated user information
 */
export interface AuthUser {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
}

/**
 * Authentication response from login endpoint
 */
export interface AuthResponse {
  user: AuthUser;
  expiresAt: string;
}

/**
 * Session status information (AUTH-05)
 */
export interface SessionStatusResponse {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
}

/**
 * User roles for authorization (Issue #3690: Added SuperAdmin)
 */
export type UserRole = 'SuperAdmin' | 'Admin' | 'Editor' | 'User';

/**
 * Helper to check if user has required role
 */
export function hasRole(user: AuthUser | null, requiredRole: UserRole): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  const required = requiredRole.toLowerCase();

  // SuperAdmin has access to everything
  if (role === 'superadmin') return true;

  // Admin has access to Admin, Editor, and User
  if (role === 'admin' && required !== 'superadmin') return true;

  // Editor has access to Editor and User
  if (role === 'editor' && (required === 'editor' || required === 'user')) return true;

  // Exact role match
  return role === required;
}

/**
 * Helper to check if user has admin or editor role
 */
export function canEdit(user: AuthUser | null): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  return role === 'superadmin' || role === 'admin' || role === 'editor';
}

/**
 * Helper to check if user is SuperAdmin (Issue #3690)
 */
export function isSuperAdmin(user: AuthUser | null): boolean {
  if (!user) return false;
  return user.role.toLowerCase() === 'superadmin';
}

/**
 * Helper to check if user is Admin or above (Issue #3690)
 */
export function isAdminOrAbove(user: AuthUser | null): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  return role === 'superadmin' || role === 'admin';
}

/**
 * Helper to check if user is Editor or above (Issue #3690)
 */
export function isEditorOrAbove(user: AuthUser | null): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  return role === 'superadmin' || role === 'admin' || role === 'editor';
}
