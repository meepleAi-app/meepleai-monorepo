/**
 * Time utility functions for formatting dates and relative timestamps
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

/**
 * Format a date as relative time (e.g., "2m ago", "3h ago", "2d ago")
 *
 * @param date - The date to format
 * @param addSuffix - Whether to add "ago" suffix (default: true)
 * @returns Formatted relative time string
 *
 * @example
 * ```ts
 * formatRelativeTime(new Date(Date.now() - 120000)) // "2 minuti fa"
 * formatRelativeTime(new Date(Date.now() - 3600000)) // "circa 1 ora fa"
 * ```
 */
export function formatRelativeTime(date: Date, addSuffix = true): string {
  return formatDistanceToNow(date, {
    addSuffix,
    locale: it,
  });
}

/**
 * Format a date for message timestamps (HH:mm format)
 *
 * @param date - The date to format
 * @returns Time in HH:mm format
 *
 * @example
 * ```ts
 * formatMessageTime(new Date('2024-01-31T14:30:00')) // "14:30"
 * ```
 */
/**
 * Format an ISO date string as a short human-readable date (e.g., "Mar 1, 2026")
 *
 * @param iso - ISO 8601 date string
 * @returns Formatted date string, or the raw input if parsing fails
 *
 * @example
 * ```ts
 * formatShortDate('2026-03-01T00:00:00Z') // "Mar 1, 2026"
 * ```
 */
export function formatShortDate(iso: string): string {
  try {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return iso;
  }
}

export function formatMessageTime(date: Date): string {
  return new Intl.DateTimeFormat('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}
