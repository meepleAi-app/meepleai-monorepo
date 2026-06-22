/**
 * formatFileSize — #1676 scope (a) F1 (mockup `sp5-admin-kb.html` L147).
 *
 * Pretty-print a byte count as a human-readable string with adaptive unit:
 *   - bytes  (< 1024)          → "873 B"
 *   - KB     (< 1024 * 1024)   → "12.4 KB"
 *   - MB     (< 1024^3)        → "8.4 MB"
 *   - GB     (>= 1024^3)       → "1.2 GB"
 *
 * Decimals:
 *   - 0 for raw bytes (no fractional bytes)
 *   - 1 for KB/MB/GB (mockup convention "8.4 MB")
 *
 * Edge cases:
 *   - 0 → "0 B"
 *   - negative input → "0 B" (defensive; bytes can't be negative in our DB)
 *   - non-finite input (NaN, Infinity) → "—"
 */

const EM_DASH = '—';
const KB = 1024;
const MB = KB * 1024;
const GB = MB * 1024;

export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes)) return EM_DASH;
  if (bytes <= 0) return '0 B';

  if (bytes < KB) return `${Math.round(bytes)} B`;
  if (bytes < MB) return `${(bytes / KB).toFixed(1)} KB`;
  if (bytes < GB) return `${(bytes / MB).toFixed(1)} MB`;
  return `${(bytes / GB).toFixed(1)} GB`;
}
