'use client';

/**
 * PdfList - Displays existing PDF documents for a game
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 * Epic #4817: User Collection Wizard
 */

import { FileText, CheckCircle2, Loader2, AlertCircle } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

export interface PdfListProps {
  documents: PdfDocumentDto[];
  loading?: boolean;
  className?: string;
}

const STATUS_CONFIG: Record<string, { label: string; color: string; icon: typeof CheckCircle2 }> = {
  Completed: { label: 'Pronto', color: 'text-green-500 bg-green-500/10', icon: CheckCircle2 },
  Ready: { label: 'Pronto', color: 'text-green-500 bg-green-500/10', icon: CheckCircle2 },
  Processing: { label: 'In elaborazione', color: 'text-amber-500 bg-amber-500/10', icon: Loader2 },
  Extracting: { label: 'Estrazione', color: 'text-blue-500 bg-blue-500/10', icon: Loader2 },
  Chunking: { label: 'Suddivisione', color: 'text-blue-500 bg-blue-500/10', icon: Loader2 },
  Embedding: { label: 'Embedding', color: 'text-indigo-500 bg-indigo-500/10', icon: Loader2 },
  Failed: { label: 'Errore', color: 'text-red-500 bg-red-500/10', icon: AlertCircle },
  Pending: { label: 'In attesa', color: 'text-slate-400 bg-slate-400/10', icon: Loader2 },
};

function getStatusConfig(status: string) {
  return STATUS_CONFIG[status] ?? STATUS_CONFIG.Pending;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function PdfList({ documents, loading, className }: PdfListProps) {
  if (loading) {
    return (
      <div
        className={cn('flex items-center gap-2 py-4 text-sm text-slate-400', className)}
        data-testid="pdf-list-loading"
      >
        <Loader2 className="h-4 w-4 animate-spin" />
        Caricamento documenti...
      </div>
    );
  }

  if (documents.length === 0) {
    return (
      <div
        className={cn('py-4 text-center text-sm text-slate-500', className)}
        data-testid="pdf-list-empty"
      >
        Nessun PDF disponibile per questo gioco.
      </div>
    );
  }

  return (
    <div className={cn('space-y-2', className)} data-testid="pdf-list">
      {documents.map((doc) => {
        const status = getStatusConfig(doc.processingStatus);
        const StatusIcon = status.icon;
        const isProcessing = ['Processing', 'Extracting', 'Chunking', 'Embedding', 'Pending'].includes(
          doc.processingStatus,
        );

        return (
          <div
            key={doc.id}
            className="flex items-center gap-3 rounded-lg border border-slate-700/50 bg-slate-800/50 px-3 py-2.5"
            data-testid={`pdf-item-${doc.id}`}
          >
            <FileText className="h-5 w-5 shrink-0 text-slate-400" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-slate-200">{doc.fileName}</p>
              <div className="flex items-center gap-2 text-xs text-slate-500">
                {doc.pageCount && <span>{doc.pageCount} pagine</span>}
                <span>{formatFileSize(doc.fileSizeBytes)}</span>
              </div>
            </div>
            <div
              className={cn(
                'flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
                status.color,
              )}
            >
              <StatusIcon className={cn('h-3 w-3', isProcessing && 'animate-spin')} />
              {status.label}
            </div>
          </div>
        );
      })}
    </div>
  );
}
