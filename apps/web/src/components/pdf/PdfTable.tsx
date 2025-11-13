import { FileText, RotateCw } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow
} from '@/components/ui/table';
import { SkeletonLoader } from '@/components/loading';

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
      minute: '2-digit'
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
    es: 'Español'
  };

  const code = (languageCode ?? 'en').toUpperCase();
  const name = languages[languageCode ?? 'en'] ?? 'Unknown';

  return { code, name };
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
  onOpenLog
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
            {pdfs.map((pdf) => {
              const { code, name } = getLanguageDisplay(pdf.language);
              const isRetrying = retryingPdfId === pdf.id;

              return (
                <TableRow key={pdf.id}>
                  <TableCell className="font-medium">{pdf.fileName}</TableCell>
                  <TableCell>
                    <Badge variant="secondary" title={name}>
                      {code}
                    </Badge>
                  </TableCell>
                  <TableCell>{formatFileSize(pdf.fileSizeBytes)}</TableCell>
                  <TableCell className="text-sm">{formatDate(pdf.uploadedAt)}</TableCell>
                  <TableCell>
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
                  </TableCell>
                  <TableCell>
                    <div className="flex gap-2">
                      {onOpenLog && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => onOpenLog(pdf)}
                        >
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
                          <RotateCw
                            className={`w-4 h-4 mr-1 ${isRetrying ? 'animate-spin' : ''}`}
                          />
                          {isRetrying ? 'Retrying...' : 'Retry'}
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </Card>
  );
}
