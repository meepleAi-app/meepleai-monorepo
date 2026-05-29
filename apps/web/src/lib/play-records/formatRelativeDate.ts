/**
 * formatRelativeDate — Task 0.2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Italian-locale aware relative date formatter for play-records UI.
 *
 * Buckets:
 *   - same calendar day                 → "oggi"
 *   - exactly 1 calendar day ago        → "ieri"
 *   - 2..30 calendar days ago           → "N giorni fa" (date-fns formatDistanceToNowStrict, addSuffix)
 *   - future date (within 30 days)      → "tra N giorn{i,o}" (EC-7)
 *   - more than 30 days ago             → short Italian date "5 mag" (date-fns format, locale=it)
 *   - null / undefined / invalid input  → "—" (em dash)
 *
 * NB. The "today/yesterday" check operates on **calendar days** in local TZ,
 * not on rolling 24h windows — matching how Italian speakers actually map
 * "ieri" (= the day before today, regardless of hour).
 *
 * Locale is hardcoded `it` because the FE app currently ships an Italian-only
 * UI surface. When `en.json` parity for play-records lands (cross-task K15),
 * this helper should accept an optional `locale` arg or read from `useLocale()`
 * at the call-site — kept as a follow-up to keep this primitive pure.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 2
 */
import { differenceInCalendarDays, format, formatDistanceToNowStrict, isValid } from 'date-fns';
import { it } from 'date-fns/locale';

const EM_DASH = '—';
const SHORT_DATE_THRESHOLD_DAYS = 30;

/**
 * Format an ISO-8601 datetime string as a human-readable Italian relative date.
 *
 * @param input — ISO-8601 datetime string, `Date` instance, or null.
 * @returns the formatted label, or `"—"` when input is null/invalid.
 */
export function formatRelativeDate(input: string | Date | null | undefined): string {
  if (input === null || input === undefined) {
    return EM_DASH;
  }

  const date = typeof input === 'string' ? new Date(input) : input;
  if (!isValid(date)) {
    return EM_DASH;
  }

  const now = new Date();
  // Negative when date is in the future; 0 = today; positive = past.
  const daysDelta = differenceInCalendarDays(now, date);

  if (daysDelta === 0) {
    return 'oggi';
  }

  if (daysDelta === 1) {
    return 'ieri';
  }

  // Past, within window
  if (daysDelta > 1 && daysDelta <= SHORT_DATE_THRESHOLD_DAYS) {
    return formatDistanceToNowStrict(date, { locale: it, addSuffix: true });
  }

  // Future (EC-7)
  if (daysDelta < 0) {
    return formatDistanceToNowStrict(date, { locale: it, addSuffix: true });
  }

  // > 30 days ago — fall back to short Italian date "5 mag"
  return format(date, 'd MMM', { locale: it });
}
