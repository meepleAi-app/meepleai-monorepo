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
export function formatMessageTime(date: Date): string {
  return new Intl.DateTimeFormat('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}
