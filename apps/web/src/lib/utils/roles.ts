/**
 * Centralized admin role check.
 * Returns true for 'admin' and 'superadmin' roles (case-insensitive).
 *
 * Usage sites: AuthModal redirect, proxy.ts middleware, RequireRole component.
 */
export function isAdminRole(role: string | null | undefined): boolean {
  if (!role) return false;
  const normalized = role.toLowerCase();
  return normalized === 'admin' || normalized === 'superadmin';
}
