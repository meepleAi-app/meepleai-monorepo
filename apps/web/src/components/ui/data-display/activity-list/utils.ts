/**
 * Activity List Utilities
 *
 * Shared utilities for the ActivityList component.
 *
 * @module components/ui/data-display/activity-list/utils
 * @see Issue #2925 - Component Library extraction
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
  SettingsIcon,
  SparklesIcon,
  FolderIcon,
  type LucideIcon,
} from 'lucide-react';

/**
 * Activity event data structure
 */
export interface ActivityItem {
  /** Unique identifier */
  id: string;
  /** Event type for icon selection */
  eventType: string;
  /** Human-readable description */
  description: string;
  /** Optional user identifier */
  userId?: string | null;
  /** Optional user email for display */
  userEmail?: string | null;
  /** Related entity identifier */
  entityId?: string | null;
  /** Related entity type */
  entityType?: string | null;
  /** ISO 8601 timestamp */
  timestamp: string;
  /** Severity level for styling */
  severity?: 'Info' | 'Warning' | 'Error' | 'Critical';
}

/**
 * Severity level styling mapping
 */
export const severityStyles: Record<string, string> = {
  Info: 'text-blue-600 bg-blue-50',
  Warning: 'text-yellow-600 bg-yellow-50',
  Error: 'text-red-600 bg-red-50',
  Critical: 'text-red-700 bg-red-100',
};

/**
 * Event type to icon mapping
 */
export const eventIcons: Record<string, LucideIcon> = {
  UserRegistered: UserPlusIcon,
  UserLogin: UserPlusIcon,
  FileUploaded: FileUpIcon,
  FileProcessed: CheckCircleIcon,
  AlertCreated: AlertTriangleIcon,
  AlertResolved: CheckCircleIcon,
  ItemAdded: ActivityIcon,
  ConfigurationChanged: SettingsIcon,
  ErrorOccurred: XCircleIcon,
  SystemEvent: ActivityIcon,
  // Legacy mappings
  PdfUploaded: FileUpIcon,
  PdfProcessed: CheckCircleIcon,
  GameAdded: FolderIcon,
};

/**
 * Activity type categories
 */
export type ActivityCategory = 'user' | 'game' | 'content' | 'system' | 'ai';

/**
 * Category-based icon configuration
 */
export const categoryIcons: Record<ActivityCategory, { icon: LucideIcon; colorClass: string }> = {
  user: { icon: UserIcon, colorClass: 'text-blue-600' },
  game: { icon: FolderIcon, colorClass: 'text-green-600' },
  content: { icon: FolderIcon, colorClass: 'text-green-600' },
  system: { icon: SettingsIcon, colorClass: 'text-stone-600' },
  ai: { icon: SparklesIcon, colorClass: 'text-purple-600' },
};

/**
 * Maps event types to activity categories
 */
export function getActivityCategory(eventType: string): ActivityCategory {
  const eventToCategory: Record<string, ActivityCategory> = {
    UserRegistered: 'user',
    UserLogin: 'user',
    GameAdded: 'game',
    ItemAdded: 'content',
    FileUploaded: 'content',
    FileProcessed: 'content',
    ConfigurationChanged: 'system',
    SystemEvent: 'system',
    ErrorOccurred: 'ai',
    PdfUploaded: 'ai',
    PdfProcessed: 'ai',
    AlertCreated: 'system',
    AlertResolved: 'system',
  };

  return eventToCategory[eventType] ?? 'system';
}

/**
 * Supported locales for timestamp formatting
 */
export type SupportedLocale = 'en' | 'it';

/**
 * Formats a timestamp as a relative time string with locale support
 */
export function formatRelativeTimestamp(timestamp: string, locale: SupportedLocale = 'en'): string {
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
