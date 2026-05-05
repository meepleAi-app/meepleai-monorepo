/**
 * OAuth URL builder helper
 *
 * Extracted from legacy OAuthButtons component.
 * Used by AuthModal (and potentially other consumers) to build
 * the backend OAuth login URL per provider.
 */

export type OAuthProvider = 'google' | 'discord' | 'github';

/**
 * Builds OAuth login URL for the given provider.
 * Reads `NEXT_PUBLIC_API_BASE` from env (fallback: http://localhost:8080).
 */
export function buildOAuthUrl(provider: OAuthProvider): string {
  const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
  return `${apiBase}/api/v1/auth/oauth/${provider}/login`;
}
