import { Check, FileText, Loader2, AlertCircle } from 'lucide-react';

import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

interface KbDocumentRowProps {
  document: GameDocument;
}

const STATUS_CONFIG = {
  indexed: { icon: Check, label: 'Indicizzato', color: 'text-green-600', testId: 'status-indexed' },
  processing: {
    icon: Loader2,
    label: 'In elaborazione...',
    color: 'text-amber-500',
    testId: 'status-processing',
  },
  failed: { icon: AlertCircle, label: 'Errore', color: 'text-red-500', testId: 'status-failed' },
} as const;

const CATEGORY_LABELS: Record<string, string> = {
  Rulebook: 'Regolamento',
  Expansion: 'Espansione',
  Errata: 'Errata',
  QuickStart: 'Quick Start',
  Reference: 'Riferimento',
  PlayerAid: 'Aiuto Giocatore',
  Other: 'Altro',
};

export function KbDocumentRow({ document }: KbDocumentRowProps) {
  const status = STATUS_CONFIG[document.status];
  const StatusIcon = status.icon;
  const isProcessing = document.status === 'processing';

  return (
    <div className="flex items-center gap-3 rounded-lg border border-border/50 bg-card/50 p-3">
      <div className="flex-shrink-0">
        <FileText className="h-5 w-5 text-muted-foreground" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm font-medium">{document.title}</span>
          {document.versionLabel && (
            <span className="flex-shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
              {document.versionLabel}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>{CATEGORY_LABELS[document.category] ?? document.category}</span>
          {document.status === 'indexed' && document.pageCount > 0 && (
            <>
              <span aria-hidden>·</span>
              <span>{document.pageCount} pagine</span>
            </>
          )}
        </div>
      </div>
      <div
        data-testid={status.testId}
        className={`flex items-center gap-1 text-xs ${status.color}`}
      >
        <StatusIcon className={`h-4 w-4 ${isProcessing ? 'animate-spin' : ''}`} />
        <span className="hidden sm:inline">{status.label}</span>
      </div>
    </div>
  );
}
