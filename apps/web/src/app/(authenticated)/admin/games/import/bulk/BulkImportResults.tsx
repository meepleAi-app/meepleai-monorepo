'use client';

/**
 * BulkImportResults - Import Summary with CSV Download
 * Issue #4175: Frontend - Bulk Import Results Summary
 *
 * Features:
 * - Stats summary: total, imported, failed
 * - Errors table with details
 * - Download CSV report button
 * - New import button (reset wizard)
 */

import {
  CheckCircle,
  Download,
  RotateCcw,
  AlertTriangle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import type { BulkImportFromJsonResult } from '@/lib/api/schemas/admin.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface BulkImportResultsProps {
  /** Import result data */
  result: BulkImportFromJsonResult;
  /** Callback to start a new import */
  onNewImport?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// CSV Export
// ============================================================================

function escapeCSVField(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

export function generateCSV(result: BulkImportFromJsonResult): string {
  const lines: string[] = [];

  // Summary section
  lines.push('Bulk Import Report');
  lines.push(`Total,${result.total}`);
  lines.push(`Enqueued,${result.enqueued}`);
  lines.push(`Skipped,${result.skipped}`);
  lines.push(`Failed,${result.failed}`);
  lines.push('');

  // Errors detail
  if (result.errors.length > 0) {
    lines.push('BGG ID,Game Name,Error Type,Reason');
    for (const err of result.errors) {
      lines.push([
        escapeCSVField(err.bggId),
        escapeCSVField(err.gameName),
        escapeCSVField(err.errorType),
        escapeCSVField(err.reason),
      ].join(','));
    }
  }

  return lines.join('\n');
}

function downloadCSV(result: BulkImportFromJsonResult): void {
  const csv = generateCSV(result);
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `bulk-import-report-${Date.now()}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

// ============================================================================
// BulkImportResults Component
// ============================================================================

export function BulkImportResults({ result, onNewImport, className }: BulkImportResultsProps) {
  const hasErrors = result.errors.length > 0;
  const isFullSuccess = result.failed === 0 && result.skipped === 0;

  return (
    <Card className={cn('w-full', className)} data-testid="bulk-import-results">
      <CardHeader>
        <div className="flex items-center gap-2">
          {isFullSuccess ? (
            <CheckCircle className="h-5 w-5 text-green-500" />
          ) : (
            <AlertTriangle className="h-5 w-5 text-amber-500" />
          )}
          <CardTitle className="text-base">Import Complete</CardTitle>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Summary Stats */}
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4" data-testid="results-stats">
          <StatBox label="Total" value={result.total} />
          <StatBox label="Enqueued" value={result.enqueued} variant="success" />
          <StatBox label="Skipped" value={result.skipped} variant="warning" />
          <StatBox label="Failed" value={result.failed} variant="error" />
        </div>

        {/* Errors Table */}
        {hasErrors && (
          <div className="space-y-2" data-testid="errors-section">
            <h4 className="text-sm font-medium">Errors & Skipped Items</h4>
            <div className="max-h-60 overflow-y-auto rounded-md border">
              <table className="w-full text-sm">
                <thead className="bg-muted/50 sticky top-0">
                  <tr>
                    <th className="px-3 py-2 text-left font-medium">BGG ID</th>
                    <th className="px-3 py-2 text-left font-medium">Game</th>
                    <th className="px-3 py-2 text-left font-medium">Type</th>
                    <th className="px-3 py-2 text-left font-medium">Reason</th>
                  </tr>
                </thead>
                <tbody>
                  {result.errors.map((err, idx) => (
                    <tr key={idx} className="border-t">
                      <td className="px-3 py-2 text-muted-foreground">{err.bggId ?? '-'}</td>
                      <td className="px-3 py-2">{err.gameName ?? '-'}</td>
                      <td className="px-3 py-2">
                        <Badge
                          variant={err.errorType === 'Duplicate' ? 'secondary' : 'destructive'}
                          className="text-xs"
                        >
                          {err.errorType}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 text-muted-foreground">{err.reason}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            onClick={() => downloadCSV(result)}
            data-testid="download-csv-button"
          >
            <Download className="h-4 w-4 mr-2" />
            Download CSV Report
          </Button>
          {onNewImport && (
            <Button variant="outline" onClick={onNewImport} data-testid="new-import-button">
              <RotateCcw className="h-4 w-4 mr-2" />
              Import Another Batch
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Helper Components
// ============================================================================

function StatBox({
  label,
  value,
  variant = 'default',
}: {
  label: string;
  value: number;
  variant?: 'default' | 'success' | 'warning' | 'error';
}) {
  const borderColors = {
    default: '',
    success: 'border-green-500/30 bg-green-500/5',
    warning: 'border-yellow-500/30 bg-yellow-500/5',
    error: 'border-red-500/30 bg-red-500/5',
  };
  const textColors = {
    default: '',
    success: 'text-green-600 dark:text-green-400',
    warning: 'text-yellow-600 dark:text-yellow-400',
    error: 'text-red-600 dark:text-red-400',
  };

  return (
    <div
      className={cn('rounded-lg border p-3 text-center', borderColors[variant])}
      data-testid={`result-stat-${label.toLowerCase()}`}
    >
      <p className={cn('text-2xl font-bold', textColors[variant])}>{value}</p>
      <p className="text-xs text-muted-foreground">{label}</p>
    </div>
  );
}
