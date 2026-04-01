/**
 * Validates that a string is a properly formatted email address.
 * Trims whitespace before checking.
 */
export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}
