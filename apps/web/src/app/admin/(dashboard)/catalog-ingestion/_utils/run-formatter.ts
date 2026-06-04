export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  const m = Math.floor(ms / 60000);
  const s = Math.floor((ms % 60000) / 1000);
  return `${m}m ${s.toString().padStart(2, '0')}s`;
}

export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const diff = (now.getTime() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return `${Math.floor(diff)}s fa`;
  if (diff < 3600) return `${Math.floor(diff / 60)} min fa`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h fa`;
  return `${Math.floor(diff / 86400)}gg fa`;
}

/**
 * Parses a .NET TimeSpan ISO string like "00:04:18" or "00:04:18.5000000"
 * into milliseconds. Returns null if input is null or unparseable.
 *
 * Note: .NET serializes TimeSpan as "[d.]hh:mm:ss[.fffffff]" — we handle
 * the common 3-segment "hh:mm:ss" form. Day-prefix ("1.00:04:18") and
 * fractional-seconds suffix are tolerated by the split + Number parse.
 */
export function parseTimeSpanToMs(ts: string | null): number | null {
  if (ts === null || ts === undefined) return null;

  // Handle optional day-prefix "d.hh:mm:ss"
  let rest = ts;
  let days = 0;
  if (ts.includes('.') && ts.split('.')[0].length <= 4 && !ts.split('.')[0].includes(':')) {
    const [dayPart, ...timeParts] = ts.split('.');
    days = Number.parseInt(dayPart, 10);
    rest = timeParts.join('.');
  }

  // "hh:mm:ss[.fffffff]"
  const segments = rest.split(':');
  if (segments.length !== 3) return null;
  const [hStr, mStr, sStr] = segments;
  const h = Number.parseInt(hStr, 10);
  const m = Number.parseInt(mStr, 10);
  const s = Number.parseFloat(sStr);
  if (Number.isNaN(h) || Number.isNaN(m) || Number.isNaN(s)) return null;

  return Math.round((days * 86400 + h * 3600 + m * 60) * 1000 + s * 1000);
}
