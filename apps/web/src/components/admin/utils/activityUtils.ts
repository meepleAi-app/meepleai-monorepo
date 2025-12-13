/**
 * Shared Activity Utilities - Issue #911 (DRY Refactor)
 *
 * Common utilities for activity timeline and feed components.
 * Extracted to eliminate code duplication and centralize activity-related logic.
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import {
  UserPlusIcon,
  FileUpIcon,
  AlertTriangleIcon,
  CheckCircleIcon,
  XCircleIcon,
  ActivityIcon,
  type LucideIcon,
} from 'lucide-react';

/**
 * Severity level styling mapping for activity events.
 * Maps severity levels to Tailwind CSS classes for consistent visual representation.
 */
export const severityStyles = {
  Info: 'text-blue-600 bg-blue-50',
  Warning: 'text-yellow-600 bg-yellow-50',
  Error: 'text-red-600 bg-red-50',
  Critical: 'text-red-700 bg-red-100',
} as const;

/**
 * Event type to icon component mapping.
 * Maps event types to corresponding Lucide icons for visual identification.
 */
export const eventIcons: Record<string, LucideIcon> = {
  UserRegistered: UserPlusIcon,
  UserLogin: UserPlusIcon,
  PdfUploaded: FileUpIcon,
  PdfProcessed: CheckCircleIcon,
  AlertCreated: AlertTriangleIcon,
  AlertResolved: CheckCircleIcon,
  GameAdded: ActivityIcon,
  ConfigurationChanged: ActivityIcon,
  ErrorOccurred: XCircleIcon,
  SystemEvent: ActivityIcon,
};

/**
 * Formats a timestamp as a relative time string in Italian.
 * Falls back to ISO string if parsing fails.
 *
 * @param timestamp - ISO 8601 timestamp string
 * @returns Relative time string in Italian (e.g., "5 minuti fa")
 *
 * @example
 * ```ts
 * formatRelativeTimestamp('2025-12-11T10:00:00Z')
 * // Returns: "2 ore fa"
 * ```
 */
export function formatRelativeTimestamp(timestamp: string): string {
  try {
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) {
      return timestamp;
    }
    return formatDistanceToNow(date, { addSuffix: true, locale: it });
  } catch {
    return timestamp;
  }
}
