'use client';

import { Loader2, RotateCcw, Trash2, FileText } from 'lucide-react';

import type { PdfDocumentSummary } from '@/components/admin/sandbox/contexts/SourceContext';
import { Badge } from '@/components/ui/data-display/badge';
import {
  AlertDialog,
  AlertDialogTrigger,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogAction,
  AlertDialogCancel,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';

interface DocumentRowProps {
  doc: PdfDocumentSummary;
  onReindex: (id: string) => void;
  onDelete: (id: string) => void;
}

const STATUS_CONFIG: Record<
  PdfDocumentSummary['status'],
  { label: string; className: string; showSpinner: boolean }
> = {
  Completed: {
    label: 'Completato',
    className: 'bg-green-100 text-green-800 border-green-200',
    showSpinner: false,
  },
  Extracting: {
    label: 'Estrazione',
    className: 'bg-amber-100 text-amber-800 border-amber-200',
    showSpinner: true,
  },
  Chunking: {
    label: 'Chunking',
    className: 'bg-amber-100 text-amber-800 border-amber-200',
    showSpinner: true,
  },
  Embedding: {
    label: 'Embedding',
    className: 'bg-amber-100 text-amber-800 border-amber-200',
    showSpinner: true,
  },
  Failed: {
    label: 'Fallito',
    className: 'bg-red-100 text-red-800 border-red-200',
    showSpinner: false,
  },
  Pending: {
    label: 'In attesa',
    className: 'bg-gray-100 text-gray-700 border-gray-200',
    showSpinner: false,
  },
};

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function DocumentRow({ doc, onReindex, onDelete }: DocumentRowProps) {
  const config = STATUS_CONFIG[doc.status];
  const isProcessing = config.showSpinner;

  return (
    <div className="flex items-center gap-3 rounded-lg border bg-white/50 px-3 py-2.5">
      <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />

      <div className="flex flex-col min-w-0 flex-1">
        <span className="font-nunito text-sm font-medium truncate" title={doc.fileName}>
          {doc.fileName}
        </span>
        <span className="font-nunito text-xs text-muted-foreground">
          {formatFileSize(doc.fileSize)}
          {' \u00b7 '}
          {doc.chunkCount} chunk
        </span>
      </div>

      <Badge variant="outline" className={config.className}>
        {config.showSpinner && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
        {config.label}
      </Badge>

      <div className="flex items-center gap-1 shrink-0">
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7"
          disabled={isProcessing}
          onClick={() => onReindex(doc.id)}
          aria-label="Reindicizza"
        >
          <RotateCcw className="h-3.5 w-3.5" />
        </Button>

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7 text-destructive hover:text-destructive"
              aria-label="Elimina"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Elimina documento</AlertDialogTitle>
              <AlertDialogDescription>
                Sei sicuro di voler eliminare &ldquo;{doc.fileName}&rdquo;? Questa azione
                rimuover&agrave; anche tutti i chunk e i vettori associati.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Annulla</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => onDelete(doc.id)}
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              >
                Elimina
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
