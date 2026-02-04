/**
 * Shared Activity Utilities - Issue #911 (DRY Refactor)
 *
 * Common utilities for activity timeline and feed components.
 * Extracted to eliminate code duplication and centralize activity-related logic.
 */

import { formatDistanceToNow } from 'date-fns';
import { enUS, it } from 'date-fns/locale';
import {
  UserPlusIcon,
  FileUpIcon,
  AlertTriangleIcon,
  CheckCircleIcon,
  XCircleIcon,
  ActivityIcon,
  UserIcon,
  GamepadIcon,
  SettingsIcon,
  SparklesIcon,
  type LucideIcon,
} from 'lucide-react';

/**
 * Activity event data structure shared across all activity components.
 * Centralized type definition to prevent duplication (Issue #2787 code review).
 */
export interface ActivityEvent {
  id: string;
  eventType: string;
  description: string;
  userId?: string | null;
  userEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  timestamp: string;
  severity?: 'Info' | 'Warning' | 'Error' | 'Critical';
}

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
 * Activity type categories for the new design system (Issue #2787).
 * Maps event types to high-level category types with colored icons.
 */
export type ActivityType = 'user' | 'game' | 'system' | 'ai';

/**
 * Category-based icon configuration with Tailwind color classes (Issue #2787).
 */
export const typeIcons: Record<ActivityType, { icon: LucideIcon; colorClass: string }> = {
  user: { icon: UserIcon, colorClass: 'text-blue-600' },
  game: { icon: GamepadIcon, colorClass: 'text-green-600' },
  system: { icon: SettingsIcon, colorClass: 'text-stone-600' },
  ai: { icon: SparklesIcon, colorClass: 'text-purple-600' },
};

/**
 * Maps event types to activity type categories (Issue #2787).
 * Used to determine which category icon to display for each event type.
 */
export function getActivityType(eventType: string): ActivityType {
  const eventToType: Record<string, ActivityType> = {
    UserRegistered: 'user',
    UserLogin: 'user',
    GameAdded: 'game',
    ConfigurationChanged: 'system',
    SystemEvent: 'system',
    ErrorOccurred: 'ai',
    PdfUploaded: 'ai',
    PdfProcessed: 'ai',
    AlertCreated: 'system',
    AlertResolved: 'system',
  };

   
  return eventToType[eventType] ?? 'system';
}

/**
 * Formats a timestamp as a relative time string with locale support.
 * Falls back to ISO string if parsing fails.
 *
 * @param timestamp - ISO 8601 timestamp string
 * @param locale - UI locale for formatting ('en' | 'it')
 * @returns Relative time string in the specified locale
 *
 * @example
 * ```ts
 * formatRelativeTimestamp('2025-12-11T10:00:00Z', 'en')
 * // Returns: "2 hours ago"
 *
 * formatRelativeTimestamp('2025-12-11T10:00:00Z', 'it')
 * // Returns: "2 ore fa"
 * ```
 */
export function formatRelativeTimestamp(timestamp: string, locale: 'en' | 'it' = 'en'): string {
  try {
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) {
      return timestamp;
    }
    const dateFnsLocale = locale === 'it' ? it : enUS;
    return formatDistanceToNow(date, { addSuffix: true, locale: dateFnsLocale });
  } catch {
    return timestamp;
  }
}
