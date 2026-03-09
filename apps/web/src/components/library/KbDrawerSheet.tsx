/**
 * KbDrawerSheet - Knowledge Base Drawer for Library Cards
 *
 * Right-side Sheet that displays all KB documents for a game.
 * Triggered by clicking the KB badge on MeepleLibraryGameCard.
 * Shows document list with type, status, size, and PDF preview.
 *
 * Uses React Query with shared 'kb-docs' key for cache consistency
 * with MeepleLibraryGameCard's card back preview.
 */

'use client';

import { useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, FileText, Loader2, RefreshCw, Upload, AlertCircle } from 'lucide-react';

import { PdfUploadModal } from '@/components/library/PdfUploadModal';
import { PdfViewerModal } from '@/components/pdf/PdfViewerModal';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import { cn } from '@/lib/utils';

import {
  formatFileSize,
  documentTypeLabels,
  documentTypeColors,
  getStatusIndicator,
  isDocumentReady,
} from './kb-utils';

// ============================================================================
// Types
// ============================================================================

export interface KbDrawerSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  gameTitle: string;
}

// ============================================================================
// Component
// ============================================================================

export function KbDrawerSheet({ open, onOpenChange, gameId, gameTitle }: KbDrawerSheetProps) {
  const queryClient = useQueryClient();
  const [uploadOpen, setUploadOpen] = useState(false);
  const [previewPdf, setPreviewPdf] = useState<{ url: string; name: string } | null>(null);

  // Shared React Query key — same data used by card back preview in MeepleLibraryGameCard
  const {
    data: documents = [],
    isLoading: loading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['kb-docs', gameId],
    queryFn: () => api.documents.getDocumentsByGame(gameId),
    enabled: open && !!gameId,
    staleTime: 2 * 60 * 1000, // 2 min
  });

  const handleUploadSuccess = () => {
    queryClient.invalidateQueries({ queryKey: ['kb-docs', gameId] });
    queryClient.invalidateQueries({ queryKey: ['library'] });
  };

  const handlePreview = (doc: PdfDocumentDto) => {
    const url = api.pdf.getPdfDownloadUrl(doc.id);
    setPreviewPdf({ url, name: doc.fileName });
  };

  const readyCount = documents.filter(d => isDocumentReady(d)).length;

  return (
    <>
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent side="right" className={cn('sm:max-w-lg', 'bg-white/80 backdrop-blur-xl')}>
          <SheetHeader className="pb-4 border-b border-border/30">
            <SheetTitle className="font-quicksand text-lg">
              <span className="text-[hsl(25,95%,45%)]">KB</span>{' '}
              <span className="text-card-foreground">{gameTitle}</span>
            </SheetTitle>
            <div className="flex items-center gap-3 mt-1">
              <span className="text-xs text-muted-foreground font-nunito">
                {documents.length} {documents.length === 1 ? 'documento' : 'documenti'}
                {readyCount > 0 && ` · ${readyCount} indicizzati`}
              </span>
              <div className="flex-1" />
              <Button
                variant="ghost"
                size="sm"
                onClick={() => refetch()}
                disabled={loading}
                className="h-7 px-2"
              >
                <RefreshCw className={cn('h-3.5 w-3.5', loading && 'animate-spin')} />
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setUploadOpen(true)}
                className="h-7 gap-1.5 text-xs font-nunito"
              >
                <Upload className="h-3.5 w-3.5" />
                Carica PDF
              </Button>
            </div>
          </SheetHeader>

          <div className="mt-4 flex flex-col gap-3 overflow-y-auto max-h-[calc(100vh-180px)]">
            {/* Loading */}
            {loading && documents.length === 0 && (
              <div className="flex flex-col items-center justify-center py-12 gap-2">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                <p className="text-sm text-muted-foreground font-nunito">Caricamento...</p>
              </div>
            )}

            {/* Error */}
            {error && (
              <div className="flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-700">
                <AlertCircle className="h-4 w-4 flex-shrink-0" />
                Errore nel caricamento dei documenti
              </div>
            )}

            {/* Empty state */}
            {!loading && !error && documents.length === 0 && (
              <div className="flex flex-col items-center justify-center py-12 gap-3 text-center">
                <div className="rounded-full bg-muted/50 p-4">
                  <FileText className="h-8 w-8 text-muted-foreground" />
                </div>
                <div>
                  <p className="font-quicksand font-semibold text-card-foreground">
                    Nessun documento
                  </p>
                  <p className="text-sm text-muted-foreground font-nunito mt-1">
                    Carica un PDF per creare la Knowledge Base
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setUploadOpen(true)}
                  className="mt-2 gap-1.5"
                >
                  <Upload className="h-4 w-4" />
                  Carica PDF
                </Button>
              </div>
            )}

            {/* Document list */}
            {documents.map(doc => {
              const status = getStatusIndicator(
                doc.processingState || doc.processingStatus || 'Pending'
              );
              const ready = isDocumentReady(doc);

              return (
                <div
                  key={doc.id}
                  className={cn(
                    'flex items-center gap-3 rounded-xl p-3',
                    'bg-[rgba(45,42,38,0.04)] hover:bg-[rgba(45,42,38,0.06)]',
                    'transition-colors'
                  )}
                >
                  {/* Status dot */}
                  <span className={cn('h-2.5 w-2.5 rounded-full flex-shrink-0', status.dot)} />

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-card-foreground truncate font-nunito">
                      {doc.fileName}
                    </p>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span
                        className={cn(
                          'rounded px-1.5 py-0.5 text-xs font-medium',
                          documentTypeColors[doc.documentType] || documentTypeColors.base
                        )}
                      >
                        {documentTypeLabels[doc.documentType] || 'Documento'}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {formatFileSize(doc.fileSizeBytes)}
                      </span>
                      {!ready && (
                        <span className={cn('text-xs font-medium', status.color)}>
                          {status.label}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Preview button */}
                  {ready && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handlePreview(doc)}
                      className="h-8 w-8 p-0 flex-shrink-0"
                      title="Anteprima PDF"
                    >
                      <Eye className="h-4 w-4 text-muted-foreground" />
                    </Button>
                  )}
                </div>
              );
            })}
          </div>
        </SheetContent>
      </Sheet>

      {/* Upload modal */}
      <PdfUploadModal
        isOpen={uploadOpen}
        onClose={() => setUploadOpen(false)}
        gameId={gameId}
        gameTitle={gameTitle}
        onUploadSuccess={handleUploadSuccess}
      />

      {/* PDF preview */}
      {previewPdf && (
        <PdfViewerModal
          open={!!previewPdf}
          onOpenChange={v => !v && setPreviewPdf(null)}
          pdfUrl={previewPdf.url}
          documentName={previewPdf.name}
        />
      )}
    </>
  );
}
