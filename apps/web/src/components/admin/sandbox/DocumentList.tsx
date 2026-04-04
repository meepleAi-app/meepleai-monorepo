'use client';

import { FileX } from 'lucide-react';

import type { PdfDocumentSummary } from '@/components/admin/sandbox/contexts/SourceContext';
import { DocumentRow } from '@/components/admin/sandbox/DocumentRow';

interface DocumentListProps {
  documents: PdfDocumentSummary[];
  onReindex: (id: string) => void;
  onDelete: (id: string) => void;
}

export function DocumentList({ documents, onReindex, onDelete }: DocumentListProps) {
  if (documents.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed bg-white/30 py-8">
        <FileX className="h-8 w-8 text-muted-foreground/50" />
        <p className="font-nunito text-sm text-muted-foreground">Nessun documento caricato</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      {documents.map(doc => (
        <DocumentRow key={doc.id} doc={doc} onReindex={onReindex} onDelete={onDelete} />
      ))}
    </div>
  );
}
