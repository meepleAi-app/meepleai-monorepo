/**
 * relative-time — shared relative-time bucketing helper for /toolkit/history
 * (Issue #3006, Task A5 EXTRA SCOPE).
 *
 * `HistoryTable` (Task A4) and `HistoryCards` (Task A5) both render a
 * relative date string (e.g. "3 giorni fa") next to each session's absolute
 * date. This module is the single source of truth for that computation so
 * both views produce identical labels for identical timestamps — it was
 * previously duplicated as an inline `getRelativeTimeParts` in
 * `HistoryTable.tsx`.
 *
 * Pure, no React, no i18n: given an ISO timestamp and an explicit `now: Date`,
 * returns an `Intl.RelativeTimeFormat` value/unit pair. Callers pass the pair
 * straight to `formatRelativeTime(value, unit)` (react-intl `IntlShape`,
 * exposed by `useTranslation`), which renders the localized label.
 *
 * Bucketing cascade: minute (< 60min) → hour (< 24h) → day (< 30d) →
 * month (< 12mo, 30-day months) → year (12mo). The escalation decision is
 * made on the ROUNDED value of the current unit, not the raw fractional
 * diff — fixes an off-by-one where e.g. a diff of 59.6 minutes used to
 * render as "60 minutes" (raw 59.6 < 60 picked the minute bucket, then the
 * *value* was rounded up to 60 afterwards). Now the rounded minute value
 * (60) is compared against the 60 threshold, so it correctly escalates to
 * the hour bucket and renders as "1 hour" instead.
 */

export interface RelativeTimeParts {
  value: number;
  unit: Intl.RelativeTimeFormatUnit;
}

const MS_PER_MINUTE = 60_000;
const MS_PER_HOUR = 60 * MS_PER_MINUTE;
const MS_PER_DAY = 24 * MS_PER_HOUR;
const MS_PER_MONTH = 30 * MS_PER_DAY;
const MS_PER_YEAR = 12 * MS_PER_MONTH;

/**
 * Buckets an ISO timestamp relative to `now` into an
 * `Intl.RelativeTimeFormat` value/unit pair, rounding to the nearest whole
 * unit and escalating to the next unit whenever the rounded value would
 * reach/exceed that unit's threshold (60 minutes, 24 hours, 30 days, 12
 * months).
 */
export function getRelativeTimeParts(iso: string, now: Date): RelativeTimeParts {
  const diffMs = new Date(iso).getTime() - now.getTime();

  const minutes = Math.round(diffMs / MS_PER_MINUTE);
  if (Math.abs(minutes) < 60) return { value: minutes, unit: 'minute' };

  const hours = Math.round(diffMs / MS_PER_HOUR);
  if (Math.abs(hours) < 24) return { value: hours, unit: 'hour' };

  const days = Math.round(diffMs / MS_PER_DAY);
  if (Math.abs(days) < 30) return { value: days, unit: 'day' };

  const months = Math.round(diffMs / MS_PER_MONTH);
  if (Math.abs(months) < 12) return { value: months, unit: 'month' };

  const years = Math.round(diffMs / MS_PER_YEAR);
  return { value: years, unit: 'year' };
}
