/* eslint-disable security/detect-object-injection -- Safe status badge config Record access */
import React from 'react';

import { FileText, RotateCw } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { TableCell, TableRow } from '@/components/ui/data-display/table';
import { Button } from '@/components/ui/primitives/button';
import type { PdfState } from '@/types/pdf';

import { PdfStatusBadge } from './PdfStatusBadge';

interface PdfDocument {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
  language?: string | null;
  status?: string | null;
  logUrl?: string | null;
}

interface PdfTableRowProps {
  pdf: PdfDocument;
  isRetrying?: boolean;
  onRetryParsing?: (pdf: PdfDocument) => void;
  onOpenLog?: (pdf: PdfDocument) => void;
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizes[i]}`;
}

function formatDate(dateString: string): string {
  try {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return dateString;
  }
}

function getLanguageDisplay(languageCode?: string | null): { code: string; name: string } {
  const languages: Record<string, string> = {
    en: 'English',
    it: 'Italiano',
    de: 'Deutsch',
    fr: 'Français',
    es: 'Español',
  };

  const code = (languageCode ?? 'en').toUpperCase();
  const name = languages[languageCode ?? 'en'] ?? 'Unknown';

  return { code, name };
}

/**
 * PdfTableRow - Memoized row component for virtualized PDF table
 *
 * Used with react-window FixedSizeList for performance optimization
 */
export const PdfTableRow = React.memo(function PdfTableRow({
  pdf,
  isRetrying = false,
  onRetryParsing,
  onOpenLog,
}: PdfTableRowProps) {
  const { code, name } = getLanguageDisplay(pdf.language);

  // Map status string to PdfState (Issue #4217)
  const getPdfState = (status?: string | null): PdfState | null => {
    const statusLower = status?.toLowerCase();
    const validStates: PdfState[] = ['pending', 'uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready', 'failed'];
    if (statusLower === 'completed') return 'ready';
    return validStates.find(s => s === statusLower) || null;
  };

  const pdfState = getPdfState(pdf.status);

  return (
    <TableRow>
      <TableCell className="font-medium">{pdf.fileName}</TableCell>
      <TableCell>
        <Badge variant="secondary" title={name}>
          {code}
        </Badge>
      </TableCell>
      <TableCell>{formatFileSize(pdf.fileSizeBytes)}</TableCell>
      <TableCell className="text-sm">{formatDate(pdf.uploadedAt)}</TableCell>
      <TableCell>
        {/* New: Use PdfStatusBadge if state matches (Issue #4217) */}
        {pdfState ? (
          <PdfStatusBadge state={pdfState} variant="compact" />
        ) : (
          <Badge
            variant={
              pdf.status === 'completed'
                ? 'default'
                : pdf.status === 'failed'
                  ? 'destructive'
                  : 'secondary'
            }
          >
            {pdf.status ?? 'Pending'}
          </Badge>
        )}
      </TableCell>
      <TableCell>
        <div className="flex gap-2">
          {onOpenLog && (
            <Button variant="outline" size="sm" onClick={() => onOpenLog(pdf)}>
              <FileText className="w-4 h-4 mr-1" />
              Log
            </Button>
          )}
          {onRetryParsing && (
            <Button
              variant="default"
              size="sm"
              onClick={() => onRetryParsing(pdf)}
              disabled={isRetrying}
            >
              <RotateCw className={`w-4 h-4 mr-1 ${isRetrying ? 'animate-spin' : ''}`} />
              {isRetrying ? 'Retrying...' : 'Retry'}
            </Button>
          )}
        </div>
      </TableCell>
    </TableRow>
  );
});
