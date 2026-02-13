/**
 * TimeSpan Formatting Utilities (Issue #4219)
 *
 * Utilities for formatting .NET TimeSpan strings ("HH:mm:ss.fffffff") to human-readable format.
 *
 * @example
 * ```ts
 * formatTimeSpan("00:03:45") // "3m 45s"
 * formatTimeSpan("01:30:00") // "1h 30m"
 * formatTimeSpan("00:00:12.5000000") // "12s"
 * ```
 */

/**
 * Parses a .NET TimeSpan string to total seconds
 * @param timeSpan TimeSpan string in format "HH:mm:ss" or "HH:mm:ss.fffffff"
 * @returns Total seconds, or 0 if invalid
 */
function parseTimeSpan(timeSpan: string): number {
  const parts = timeSpan.split(':');
  if (parts.length !== 3) return 0;

  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  const secondsPart = parts[2] ?? '0';
  const seconds = parseFloat(secondsPart);

  return hours * 3600 + minutes * 60 + seconds;
}

/**
 * Formats a .NET TimeSpan string to human-readable duration
 * @param timeSpan TimeSpan string from backend ("HH:mm:ss.fffffff")
 * @returns Formatted string like "3m 45s", "1h 30m", "< 1m"
 *
 * @example
 * formatTimeSpan("00:03:45") // "3m 45s"
 * formatTimeSpan("01:30:00") // "1h 30m"
 * formatTimeSpan("00:00:45") // "45s"
 * formatTimeSpan("00:00:05") // "< 1m"
 */
export function formatTimeSpan(timeSpan: string | null | undefined): string {
  if (!timeSpan) return '-';

  const totalSeconds = parseTimeSpan(timeSpan);
  if (totalSeconds === 0) return '0s';
  if (totalSeconds < 60) return totalSeconds < 10 ? '< 1m' : `${Math.round(totalSeconds)}s`;

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = Math.round(totalSeconds % 60);

  const parts: string[] = [];
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  if (seconds > 0 && hours === 0) parts.push(`${seconds}s`); // Show seconds only if < 1 hour

  return parts.join(' ');
}

/**
 * Formats estimated time remaining with "~" prefix
 * @param timeSpan TimeSpan string from backend
 * @returns Formatted ETA like "~3m 45s", "~1h 30m"
 *
 * @example
 * formatETA("00:03:45") // "~3m 45s"
 * formatETA(null) // "-"
 */
export function formatETA(timeSpan: string | null | undefined): string {
  if (!timeSpan) return '-';
  const formatted = formatTimeSpan(timeSpan);
  return formatted === '-' || formatted === '0s' ? '-' : `~${formatted}`;
}
