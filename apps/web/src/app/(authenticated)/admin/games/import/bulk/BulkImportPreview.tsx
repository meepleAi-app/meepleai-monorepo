'use client';

/**
 * BulkImportPreview Component - Issue #4356
 * Epic #4136: Admin Game Import
 *
 * Preview & validation step between JSON input and API submission.
 * Shows parsed entries, validation summary, duplicate detection,
 * and error details before confirming import.
 */

import type { JSX } from 'react';

import {
  CheckCircle,
  AlertTriangle,
  XCircle,
  ArrowLeft,
  Upload,
  Search,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/data-display/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { cn } from '@/lib/utils';

// ─── Types ───────────────────────────────────────────────────────

export interface ParsedEntry {
  index: number;
  bggId: number;
  name: string;
  isValid: boolean;
  errors: string[];
  isDuplicate: boolean;
}

export interface PreviewData {
  entries: ParsedEntry[];
  totalEntries: number;
  validCount: number;
  invalidCount: number;
  duplicateCount: number;
}

export interface BulkImportPreviewProps {
  previewData: PreviewData;
  onConfirm: () => void;
  onBack: () => void;
  isSubmitting: boolean;
}

// ─── Parse & Validate ────────────────────────────────────────────

const MAX_ENTRIES = 500;

export function parseAndValidateEntries(jsonContent: string): PreviewData | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(jsonContent);
  } catch {
    return null;
  }

  if (!Array.isArray(parsed) || parsed.length === 0) {
    return null;
  }

  const seenBggIds = new Map<number, number>(); // bggId → first index
  const entries: ParsedEntry[] = [];
  let validCount = 0;
  let invalidCount = 0;
  let duplicateCount = 0;

  for (let i = 0; i < parsed.length; i++) {
    const raw = parsed[i];
    const errors: string[] = [];
    let bggId = 0;
    let name = '';
    let isValid = true;
    let isDuplicate = false;

    if (typeof raw !== 'object' || raw === null) {
      errors.push('Entry is not an object');
      isValid = false;
    } else {
      const obj = raw as Record<string, unknown>;

      // Validate bggId
      if (typeof obj.bggId !== 'number' || !Number.isInteger(obj.bggId) || obj.bggId <= 0) {
        errors.push('bggId must be a positive integer');
        isValid = false;
      } else {
        bggId = obj.bggId;
      }

      // Validate name
      if (typeof obj.name !== 'string' || obj.name.trim() === '') {
        errors.push('name must be a non-empty string');
        isValid = false;
      } else {
        name = obj.name.trim();
      }

      // Duplicate detection within batch
      if (bggId > 0) {
        const firstIdx = seenBggIds.get(bggId);
        if (firstIdx !== undefined) {
          isDuplicate = true;
          duplicateCount++;
          errors.push(`Duplicate of entry #${firstIdx + 1}`);
        } else {
          seenBggIds.set(bggId, i);
        }
      }
    }

    if (!isValid || isDuplicate) {
      if (!isDuplicate) invalidCount++;
    } else {
      validCount++;
    }

    entries.push({ index: i, bggId, name, isValid: isValid && !isDuplicate, errors, isDuplicate });
  }

  return {
    entries,
    totalEntries: parsed.length,
    validCount,
    invalidCount,
    duplicateCount,
  };
}

// ─── Preview Table (first 20 entries) ────────────────────────────

const PREVIEW_LIMIT = 20;

function PreviewTable({ entries }: { entries: ParsedEntry[] }): JSX.Element {
  const displayed = entries.slice(0, PREVIEW_LIMIT);
  const remaining = entries.length - PREVIEW_LIMIT;

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-medium">Entry Preview</h4>
        <Badge variant="outline" className="text-xs">
          Showing {Math.min(entries.length, PREVIEW_LIMIT)} of {entries.length}
        </Badge>
      </div>
      <div className="max-h-80 overflow-y-auto rounded-md border">
        <table className="w-full text-sm" data-testid="preview-table">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-3 py-2 text-left font-medium w-12">#</th>
              <th className="px-3 py-2 text-left font-medium w-24">BGG ID</th>
              <th className="px-3 py-2 text-left font-medium">Game Name</th>
              <th className="px-3 py-2 text-left font-medium w-24">Status</th>
            </tr>
          </thead>
          <tbody>
            {displayed.map((entry) => (
              <tr
                key={entry.index}
                className={cn(
                  'border-t',
                  !entry.isValid && 'bg-red-50/50 dark:bg-red-950/20',
                  entry.isDuplicate && 'bg-yellow-50/50 dark:bg-yellow-950/20'
                )}
                data-testid={`preview-row-${entry.index}`}
              >
                <td className="px-3 py-2 text-muted-foreground text-xs">{entry.index + 1}</td>
                <td className="px-3 py-2 font-mono text-xs">{entry.bggId || '-'}</td>
                <td className="px-3 py-2">{entry.name || '-'}</td>
                <td className="px-3 py-2">
                  {entry.isDuplicate ? (
                    <Badge variant="secondary" className="text-xs">
                      <AlertTriangle className="h-3 w-3 mr-1" />
                      Duplicate
                    </Badge>
                  ) : entry.isValid ? (
                    <Badge variant="outline" className="text-xs text-green-600 dark:text-green-400 border-green-500/30">
                      <CheckCircle className="h-3 w-3 mr-1" />
                      Valid
                    </Badge>
                  ) : (
                    <Badge variant="destructive" className="text-xs">
                      <XCircle className="h-3 w-3 mr-1" />
                      Invalid
                    </Badge>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {remaining > 0 && (
        <p className="text-xs text-muted-foreground text-center">
          +{remaining} more {remaining === 1 ? 'entry' : 'entries'} not shown
        </p>
      )}
    </div>
  );
}

// ─── Error Details ───────────────────────────────────────────────

function ErrorDetails({ entries }: { entries: ParsedEntry[] }): JSX.Element | null {
  const entriesWithErrors = entries.filter((e) => e.errors.length > 0);
  if (entriesWithErrors.length === 0) return null;

  return (
    <div className="space-y-2" data-testid="error-details">
      <h4 className="text-sm font-medium flex items-center gap-2">
        <AlertTriangle className="h-4 w-4 text-yellow-500" />
        Issues Found ({entriesWithErrors.length})
      </h4>
      <div className="max-h-48 overflow-y-auto rounded-md border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-3 py-2 text-left font-medium w-16">Entry</th>
              <th className="px-3 py-2 text-left font-medium">Issue</th>
            </tr>
          </thead>
          <tbody>
            {entriesWithErrors.map((entry) =>
              entry.errors.map((err, errIdx) => (
                <tr key={`${entry.index}-${errIdx}`} className="border-t">
                  <td className="px-3 py-2 text-muted-foreground text-xs">#{entry.index + 1}</td>
                  <td className="px-3 py-2 text-sm">{err}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Main Component ──────────────────────────────────────────────

export function BulkImportPreview({
  previewData,
  onConfirm,
  onBack,
  isSubmitting,
}: BulkImportPreviewProps): JSX.Element {
  const { entries, totalEntries, validCount, invalidCount, duplicateCount } = previewData;
  const hasErrors = invalidCount > 0 || duplicateCount > 0;
  const exceedsLimit = totalEntries > MAX_ENTRIES;
  const canSubmit = validCount > 0 && !exceedsLimit;

  return (
    <div className="space-y-6" data-testid="bulk-import-preview">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="sm"
          onClick={onBack}
          disabled={isSubmitting}
          data-testid="preview-back"
        >
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back to Edit
        </Button>
        <div className="flex-1">
          <h3 className="text-lg font-semibold flex items-center gap-2">
            <Search className="h-5 w-5" />
            Import Preview
          </h3>
        </div>
      </div>

      {/* Validation Summary */}
      <Card data-testid="validation-summary">
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Validation Summary</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="rounded-lg border p-3 text-center">
              <p className="text-2xl font-bold" data-testid="total-count">{totalEntries}</p>
              <p className="text-xs text-muted-foreground">Total Entries</p>
            </div>
            <div className="rounded-lg border p-3 text-center border-green-500/30 bg-green-500/5">
              <p className="text-2xl font-bold text-green-600 dark:text-green-400" data-testid="valid-count">{validCount}</p>
              <p className="text-xs text-muted-foreground">Valid</p>
            </div>
            <div className="rounded-lg border p-3 text-center border-yellow-500/30 bg-yellow-500/5">
              <p className="text-2xl font-bold text-yellow-600 dark:text-yellow-400" data-testid="duplicate-count">{duplicateCount}</p>
              <p className="text-xs text-muted-foreground">Duplicates</p>
            </div>
            <div className="rounded-lg border p-3 text-center border-red-500/30 bg-red-500/5">
              <p className="text-2xl font-bold text-red-600 dark:text-red-400" data-testid="invalid-count">{invalidCount}</p>
              <p className="text-xs text-muted-foreground">Invalid</p>
            </div>
          </div>

          {/* Warnings */}
          {exceedsLimit && (
            <Alert variant="destructive" className="mt-4" data-testid="limit-exceeded-alert">
              <XCircle className="h-4 w-4" />
              <AlertTitle>Entry Limit Exceeded</AlertTitle>
              <AlertDescription>
                Maximum {MAX_ENTRIES} entries per import. Please reduce the number of entries.
              </AlertDescription>
            </Alert>
          )}

          {hasErrors && !exceedsLimit && (
            <Alert className="mt-4" data-testid="has-errors-alert">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Some entries have issues</AlertTitle>
              <AlertDescription>
                {invalidCount > 0 && `${invalidCount} invalid ${invalidCount === 1 ? 'entry' : 'entries'} will be skipped. `}
                {duplicateCount > 0 && `${duplicateCount} duplicate ${duplicateCount === 1 ? 'entry' : 'entries'} detected within the batch. `}
                Only {validCount} valid {validCount === 1 ? 'entry' : 'entries'} will be imported.
              </AlertDescription>
            </Alert>
          )}

          {!hasErrors && !exceedsLimit && (
            <Alert className="mt-4 border-green-500/30 bg-green-500/5" data-testid="all-valid-alert">
              <CheckCircle className="h-4 w-4 text-green-500" />
              <AlertTitle>All entries are valid</AlertTitle>
              <AlertDescription>
                {validCount} {validCount === 1 ? 'game' : 'games'} ready to import.
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      {/* Preview Table */}
      <Card>
        <CardContent className="pt-6">
          <PreviewTable entries={entries} />
        </CardContent>
      </Card>

      {/* Error Details */}
      {hasErrors && (
        <Card>
          <CardContent className="pt-6">
            <ErrorDetails entries={entries} />
          </CardContent>
        </Card>
      )}

      {/* Action Buttons */}
      <div className="flex items-center gap-3">
        <Button
          onClick={onConfirm}
          disabled={!canSubmit || isSubmitting}
          data-testid="confirm-import"
        >
          {isSubmitting ? (
            <>Importing...</>
          ) : (
            <>
              <Upload className="mr-2 h-4 w-4" />
              Confirm Import ({validCount} {validCount === 1 ? 'game' : 'games'})
            </>
          )}
        </Button>
        <Button
          variant="outline"
          onClick={onBack}
          disabled={isSubmitting}
          data-testid="preview-cancel"
        >
          Cancel
        </Button>
      </div>
    </div>
  );
}
