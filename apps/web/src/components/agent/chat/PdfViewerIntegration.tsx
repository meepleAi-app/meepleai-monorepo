/**
 * PdfViewerIntegration - PDF viewer wrapper for agent chat
 * Issue #3251 (FRONT-015)
 *
 * Features:
 * - Integrates PdfViewerModal with agent chat
 * - Handles citation clicks → PDF page navigation
 * - Keyboard shortcut 'P' to toggle
 * - Toast notifications on open
 */

'use client';

import { useEffect, useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';

import { PdfViewer } from '@/components/pdf-viewer/PdfViewer';
import {
  Dialog,
  DialogContent,
} from '@/components/ui/overlays/dialog';
import { useAgentStore } from '@/stores/agentStore';

// ============================================================================
// Types
// ============================================================================

interface GameDocument {
  id: string;
  fileName: string;
  pdfUrl: string;
  isActive: boolean;
  pageCount: number;
}

interface PdfViewerIntegrationProps {
  /** Game ID to fetch documents for */
  gameId: string;
}

// ============================================================================
// Component
// ============================================================================

export function PdfViewerIntegration({ gameId }: PdfViewerIntegrationProps): React.JSX.Element | null {
  const {
    pdfViewerOpen,
    pdfViewerPage,
    pdfViewerDocumentId,
    closePdfViewer,
    setPdfViewerPage,
    openPdfViewer,
  } = useAgentStore();

  // Fetch game documents
  const { data: documents } = useQuery<GameDocument[]>({
    queryKey: ['game-documents', gameId],
    queryFn: async () => {
      const response = await fetch(`/api/v1/admin/shared-games/${gameId}/documents`);
      if (!response.ok) {
        throw new Error('Errore caricamento documenti');
      }
      return response.json();
    },
    enabled: !!gameId,
  });

  // Find active document or specific document
  const activeDocument = pdfViewerDocumentId
    ? documents?.find(d => d.id === pdfViewerDocumentId)
    : documents?.find(d => d.isActive);

  const pdfUrl = activeDocument?.pdfUrl;

  // Handle page change from viewer
  const handlePageChange = useCallback((page: number) => {
    setPdfViewerPage(page);
  }, [setPdfViewerPage]);

  // Keyboard shortcut: 'P' to toggle PDF viewer
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      // Ignore if typing in input/textarea
      if (
        event.target instanceof HTMLInputElement ||
        event.target instanceof HTMLTextAreaElement
      ) {
        return;
      }

      // 'P' key toggles PDF viewer (ignore if modifier keys are pressed)
      if ((event.key === 'p' || event.key === 'P') && !event.ctrlKey && !event.metaKey && !event.altKey) {
        if (pdfViewerOpen) {
          closePdfViewer();
        } else if (activeDocument) {
          // Open at last viewed page or page 1
          const lastPage = pdfViewerPage || 1;
          openPdfViewer(activeDocument.id, lastPage);
          toast.info(`📄 Apertura ${activeDocument.fileName}`, {
            description: `Pagina ${lastPage}`,
          });
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [pdfViewerOpen, pdfViewerPage, activeDocument, closePdfViewer, openPdfViewer]);

  // Don't render if no PDF available
  if (!pdfUrl) {
    return null;
  }

  return (
    <Dialog open={pdfViewerOpen} onOpenChange={open => !open && closePdfViewer()}>
      <DialogContent className="max-w-4xl h-[85vh] p-0 bg-slate-900 border-slate-800">
        <PdfViewer
          pdfUrl={pdfUrl}
          initialPage={pdfViewerPage || 1}
          onPageChange={handlePageChange}
          highlightedPage={pdfViewerPage || undefined}
          showControls={true}
          className="h-full"
        />
      </DialogContent>
    </Dialog>
  );
}
