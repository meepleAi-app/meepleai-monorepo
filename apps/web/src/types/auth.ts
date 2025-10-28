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
 * User roles for authorization
 */
export type UserRole = 'Admin' | 'Editor' | 'Viewer' | 'User';

/**
 * Helper to check if user has required role
 */
export function hasRole(user: AuthUser | null, requiredRole: UserRole): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  const required = requiredRole.toLowerCase();

  // Admin has access to everything
  if (role === 'admin') return true;

  // Exact role match
  return role === required;
}

/**
 * Helper to check if user has admin or editor role
 */
export function canEdit(user: AuthUser | null): boolean {
  if (!user) return false;
  const role = user.role.toLowerCase();
  return role === 'admin' || role === 'editor';
}
