/**
 * Authentication Utilities - Centralized Export
 *
 * Re-exports from server.ts for convenient imports:
 * @example
 * import { getServerUser, isAdmin } from '@/lib/auth'
 */

export { getServerUser, isAdmin, isEditor, hasRole, hasAnyRole } from './server';
