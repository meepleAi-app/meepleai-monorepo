/**
 * CSV Export Utilities
 *
 * Shared, framework-free helpers for building CSV content and triggering a
 * browser download. Promoted from the private helpers in `lib/utils/export.ts`
 * (Issue #2139) so other features (e.g. /toolkit/history "Esporta CSV") can
 * reuse the same escaping and download mechanism without duplicating it.
 *
 * Issues #3006 / #3007
 */

/**
 * Escape a single CSV field.
 *
 * Wraps the field in double quotes if it contains a comma, double quote, or
 * newline, doubling any embedded double quotes per RFC 4180. `null`/`undefined`
 * become an empty string; numbers are stringified.
 */
export function escapeCSVField(field: string | number | null | undefined): string {
  if (field === null || field === undefined) {
    return '';
  }

  const value = String(field);

  if (value.includes(',') || value.includes('"') || value.includes('\n')) {
    return `"${value.replace(/"/g, '""')}"`;
  }

  return value;
}

/**
 * Build a CSV string from headers and rows.
 *
 * Each cell is escaped via {@link escapeCSVField}; cells are joined with a
 * comma and rows are joined with CRLF (`\r\n`), per RFC 4180.
 */
export function rowsToCsv(headers: string[], rows: (string | number | null)[][]): string {
  const lines = [headers, ...rows].map(row => row.map(escapeCSVField).join(','));
  return lines.join('\r\n');
}

/**
 * Trigger a browser download for a file.
 *
 * @param content - File content
 * @param filename - Filename for download
 * @param mimeType - MIME type of the file (default: CSV)
 */
export function downloadFile(
  content: string,
  filename: string,
  mimeType: string = 'text/csv;charset=utf-8;'
): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
