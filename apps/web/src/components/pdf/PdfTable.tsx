import { FileText } from 'lucide-react';

import { Card } from '@/components/ui/card';
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table';

import { PdfTableRow } from './PdfTableRow';

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

interface PdfTableProps {
  pdfs: PdfDocument[];
  loading?: boolean;
  error?: string | null;
  retryingPdfId?: string | null;
  onRetryParsing?: (pdf: PdfDocument) => void;
  onOpenLog?: (pdf: PdfDocument) => void;
}

/**
 * PdfTable - Display uploaded PDFs with actions
 *
 * Features:
 * - Responsive table with Shadcn/UI components
 * - Empty states, loading states, error states
 * - Language badges
 * - Status display
 * - Action buttons (view log, retry parsing)
 * - Accessible table markup
 */
export function PdfTable({
  pdfs,
  loading = false,
  error = null,
  retryingPdfId = null,
  onRetryParsing,
  onOpenLog,
}: PdfTableProps) {
  if (loading) {
    return (
      <Card className="p-4">
        <h3 className="text-lg font-semibold mb-4">Uploaded PDFs</h3>
        <div role="status" aria-live="polite" className="space-y-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <div
              key={`pdf-skeleton-${index}`}
              className="h-12 bg-muted rounded-md animate-pulse"
              aria-label="Loading PDFs"
            />
          ))}
        </div>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className="p-4">
        <h3 className="text-lg font-semibold mb-4">Uploaded PDFs</h3>
        <p className="text-sm text-destructive">{error}</p>
      </Card>
    );
  }

  if (pdfs.length === 0) {
    return (
      <Card className="p-4">
        <h3 className="text-lg font-semibold mb-4">Uploaded PDFs</h3>
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <FileText className="w-12 h-12 text-muted-foreground mb-3" />
          <p className="text-sm text-muted-foreground">No PDFs uploaded yet for this game.</p>
        </div>
      </Card>
    );
  }

  return (
    <Card className="p-4">
      <h3 className="text-lg font-semibold mb-4">Uploaded PDFs</h3>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>File name</TableHead>
              <TableHead>Language</TableHead>
              <TableHead>Size</TableHead>
              <TableHead>Uploaded</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {pdfs.map(pdf => (
              <PdfTableRow
                key={pdf.id}
                pdf={pdf}
                isRetrying={retryingPdfId === pdf.id}
                onRetryParsing={onRetryParsing}
                onOpenLog={onOpenLog}
              />
            ))}
          </TableBody>
        </Table>
      </div>
    </Card>
  );
}
