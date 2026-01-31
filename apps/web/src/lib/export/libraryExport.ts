/**
 * Library Export Utilities (Issue #2611)
 *
 * Functions for exporting user library data to CSV and JSON formats.
 * Supports both minimal and full export options.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ============================================================================
// Types
// ============================================================================

export type ExportFormat = 'csv' | 'json';
export type ExportScope = 'minimal' | 'full';

export interface ExportOptions {
  format: ExportFormat;
  scope: ExportScope;
  filename?: string;
}

export interface ExportResult {
  content: string;
  filename: string;
  mimeType: string;
}

// ============================================================================
// CSV Formatting
// ============================================================================

/**
 * Escapes a value for CSV format.
 * Handles quotes, commas, and newlines.
 */
function escapeCsvValue(value: string | null | undefined): string {
  if (value === null || value === undefined) return '';

  const str = String(value);
  // If the value contains quotes, commas, or newlines, wrap in quotes and escape internal quotes
  if (str.includes('"') || str.includes(',') || str.includes('\n') || str.includes('\r')) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

/**
 * Formats a date to ISO 8601 date string (YYYY-MM-DD).
 */
function formatDate(isoString: string): string {
  try {
    return new Date(isoString).toISOString().split('T')[0];
  } catch {
    return '';
  }
}

/**
 * Formats a boolean for display in CSV.
 */
function formatBoolean(value: boolean): string {
  return value ? 'Sì' : 'No';
}

/**
 * Converts library entries to CSV format.
 */
export function toCsv(data: UserLibraryEntry[], scope: ExportScope): string {
  if (data.length === 0) {
    return scope === 'minimal'
      ? '"Titolo Gioco","Data Aggiunta","Preferito","Note"'
      : '"Titolo Gioco","Data Aggiunta","Preferito","Note","Editore","Anno Pubblicazione","ID Gioco"';
  }

  const minimalHeaders = ['Titolo Gioco', 'Data Aggiunta', 'Preferito', 'Note'];
  const fullHeaders = [...minimalHeaders, 'Editore', 'Anno Pubblicazione', 'ID Gioco'];

  const headers = scope === 'minimal' ? minimalHeaders : fullHeaders;
  const headerRow = headers.map(h => `"${h}"`).join(',');

  const rows = data.map(entry => {
    const minimalRow = [
      escapeCsvValue(entry.gameTitle),
      escapeCsvValue(formatDate(entry.addedAt)),
      escapeCsvValue(formatBoolean(entry.isFavorite)),
      escapeCsvValue(entry.notes),
    ];

    if (scope === 'full') {
      minimalRow.push(
        escapeCsvValue(entry.gamePublisher),
        escapeCsvValue(entry.gameYearPublished?.toString()),
        escapeCsvValue(entry.gameId),
      );
    }

    return minimalRow.join(',');
  });

  return [headerRow, ...rows].join('\n');
}

// ============================================================================
// JSON Formatting
// ============================================================================

/**
 * Transforms library entries to minimal JSON format.
 */
function toMinimalJson(data: UserLibraryEntry[]): object[] {
  return data.map(entry => ({
    gameTitle: entry.gameTitle,
    addedAt: entry.addedAt,
    isFavorite: entry.isFavorite,
    notes: entry.notes ?? null,
  }));
}

/**
 * Transforms library entries to full JSON format.
 */
function toFullJson(data: UserLibraryEntry[]): object[] {
  return data.map(entry => ({
    gameTitle: entry.gameTitle,
    addedAt: entry.addedAt,
    isFavorite: entry.isFavorite,
    notes: entry.notes ?? null,
    gameId: entry.gameId,
    gamePublisher: entry.gamePublisher ?? null,
    gameYearPublished: entry.gameYearPublished ?? null,
    gameImageUrl: entry.gameImageUrl ?? null,
  }));
}

/**
 * Converts library entries to JSON format.
 */
export function toJson(data: UserLibraryEntry[], scope: ExportScope): string {
  const transformed = scope === 'minimal' ? toMinimalJson(data) : toFullJson(data);
  return JSON.stringify(transformed, null, 2);
}

// ============================================================================
// Download Functionality
// ============================================================================

/**
 * Triggers a file download in the browser.
 */
export function downloadFile(content: string, filename: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.style.display = 'none';

  document.body.appendChild(link);
  link.click();

  // Cleanup
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

// ============================================================================
// Main Export Function
// ============================================================================

/**
 * Generates an export file from library data.
 */
export function generateExport(
  data: UserLibraryEntry[],
  options: ExportOptions
): ExportResult {
  const { format, scope, filename } = options;
  const timestamp = new Date().toISOString().split('T')[0];
  const defaultFilename = `libreria-meepleai-${timestamp}`;

  if (format === 'csv') {
    return {
      content: toCsv(data, scope),
      filename: filename ?? `${defaultFilename}.csv`,
      mimeType: 'text/csv;charset=utf-8;',
    };
  }

  return {
    content: toJson(data, scope),
    filename: filename ?? `${defaultFilename}.json`,
    mimeType: 'application/json;charset=utf-8;',
  };
}

/**
 * Exports library data to a file and triggers download.
 */
export function exportLibrary(
  data: UserLibraryEntry[],
  options: ExportOptions
): void {
  const result = generateExport(data, options);
  downloadFile(result.content, result.filename, result.mimeType);
}
