/**
 * Priority Calculation Utilities (Issue #2895)
 *
 * Client-side priority determination for approval queue items
 * based on submission age (createdAt timestamp).
 */

import { differenceInDays } from 'date-fns';

/**
 * Priority levels for approval queue items
 */
export type Priority = 'high' | 'medium' | 'low';

/**
 * Calculate priority based on days pending approval
 *
 * Rules:
 * - High (>7 days): Red indicator, urgent action needed
 * - Medium (3-7 days): Yellow indicator, needs attention soon
 * - Low (<3 days): No indicator, normal priority
 *
 * @param createdAt ISO 8601 datetime string when item was submitted
 * @returns Priority level: 'high', 'medium', or 'low'
 *
 * @example
 * calculatePriority('2026-01-20T10:00:00Z') // High if today is 2026-01-29
 * calculatePriority('2026-01-26T10:00:00Z') // Medium if today is 2026-01-29
 * calculatePriority('2026-01-28T10:00:00Z') // Low if today is 2026-01-29
 */
export function calculatePriority(createdAt: string): Priority {
  const now = new Date();
  const submittedDate = new Date(createdAt);
  const daysPending = differenceInDays(now, submittedDate);

  if (daysPending > 7) return 'high';
  if (daysPending >= 3) return 'medium';
  return 'low';
}

/**
 * Get Badge variant for priority level
 *
 * Maps priority to Badge component variant for consistent styling
 * across the application.
 *
 * @param priority Priority level
 * @returns Badge variant string
 *
 * @example
 * getPriorityBadgeVariant('high')    // 'destructive'
 * getPriorityBadgeVariant('medium')  // 'secondary'
 * getPriorityBadgeVariant('low')     // 'default'
 */
export function getPriorityBadgeVariant(
  priority: Priority
): 'destructive' | 'secondary' | 'default' {
  switch (priority) {
    case 'high':
      return 'destructive'; // Red badge
    case 'medium':
      return 'secondary'; // Yellow/amber badge
    case 'low':
      return 'default'; // Default badge (or can be omitted in UI)
  }
}

/**
 * Get human-readable priority label
 *
 * @param priority Priority level
 * @returns Italian label for the priority
 *
 * @example
 * getPriorityLabel('high')    // 'Alta priorità'
 * getPriorityLabel('medium')  // 'Media priorità'
 * getPriorityLabel('low')     // 'Priorità normale'
 */
export function getPriorityLabel(priority: Priority): string {
  switch (priority) {
    case 'high':
      return 'Alta priorità';
    case 'medium':
      return 'Media priorità';
    case 'low':
      return 'Priorità normale';
  }
}
