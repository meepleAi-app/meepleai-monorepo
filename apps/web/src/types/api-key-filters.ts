/**
 * API Key Filter Types (Issue #910)
 *
 * Type definitions for filtering API keys in admin management page.
 * Used by ApiKeyFilterPanel component.
 */

/**
 * Status of an API key
 */
export type ApiKeyStatus = 'active' | 'expired' | 'revoked' | 'all';

/**
 * API Key scope options
 */
export type ApiKeyScope = 'read' | 'write' | 'admin';

/**
 * Filter state for API Key management
 */
export interface ApiKeyFilters {
  /**
   * Filter by key name (search)
   */
  search?: string;

  /**
   * Filter by status
   */
  status?: ApiKeyStatus;

  /**
   * Filter by scopes (multiple selection)
   */
  scopes?: ApiKeyScope[];

  /**
   * Filter by creation date (from)
   */
  createdFrom?: Date;

  /**
   * Filter by creation date (to)
   */
  createdTo?: Date;

  /**
   * Filter by expiration date (from)
   */
  expiresFrom?: Date;

  /**
   * Filter by expiration date (to)
   */
  expiresTo?: Date;

  /**
   * Show only keys used in last N days
   */
  lastUsedDays?: number;
}

/**
 * Scope option with metadata for UI rendering
 */
export interface ScopeOption {
  value: ApiKeyScope;
  label: string;
  description: string;
  color: 'blue' | 'green' | 'red';
}

/**
 * Available scope options
 */
export const AVAILABLE_SCOPES: ScopeOption[] = [
  { value: 'read', label: 'Read', description: 'Read-only access', color: 'blue' },
  { value: 'write', label: 'Write', description: 'Create and update', color: 'green' },
  { value: 'admin', label: 'Admin', description: 'Full access', color: 'red' },
];

/**
 * Status option with metadata for UI rendering
 */
export interface StatusOption {
  value: ApiKeyStatus;
  label: string;
  description?: string;
}

/**
 * Available status options
 */
export const AVAILABLE_STATUSES: StatusOption[] = [
  { value: 'all', label: 'All Keys' },
  { value: 'active', label: 'Active', description: 'Currently valid' },
  { value: 'expired', label: 'Expired', description: 'Past expiration date' },
  { value: 'revoked', label: 'Revoked', description: 'Manually disabled' },
];
