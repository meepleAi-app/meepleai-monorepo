'use client';

/**
 * KnowledgeBaseStep - Step 2: PDF & Knowledge Base management
 * Issue #4820: Step 2 Knowledge Base & PDF Management
 * Epic #4817: User Collection Wizard
 *
 * Shows existing PDFs (from shared game), allows custom PDF upload,
 * displays embedding status, and provides skip option.
 */

import { useCallback, useEffect, useState } from 'react';

import { BookOpen, Upload } from 'lucide-react';

import { RagReadyIndicator } from '@/components/pdf/RagReadyIndicator';
import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';
import { cn } from '@/lib/utils';

import { PdfList } from './PdfList';
import { PdfUploadZone } from './PdfUploadZone';

export function KnowledgeBaseStep() {
  const selectedGame = useAddGameWizardStore(s => s.selectedGame);
  const setDocuments = useAddGameWizardStore(s => s.setDocuments);
  const setCustomPdfUploaded = useAddGameWizardStore(s => s.setCustomPdfUploaded);
  const customPdfUploaded = useAddGameWizardStore(s => s.customPdfUploaded);

  const [existingDocs, setExistingDocs] = useState<PdfDocumentDto[]>([]);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [uploadedDocId, setUploadedDocId] = useState<string | null>(null);

  const gameId = selectedGame?.gameId ?? null;

  // Fetch existing PDFs on mount
  useEffect(() => {
    if (!gameId) return;

    let cancelled = false;
    setLoadingDocs(true);

    api.documents
      .getDocumentsByGame(gameId)
      .then(docs => {
        if (!cancelled) {
          setExistingDocs(docs);
          setDocuments(
            docs.map(d => ({
              id: d.id,
              fileName: d.fileName,
              pageCount: d.pageCount ?? undefined,
              status: d.processingStatus,
              documentType: d.documentType,
            }))
          );
        }
      })
      .catch(() => {
        // Silently handle - the game may not have PDFs yet
        if (!cancelled) setExistingDocs([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingDocs(false);
      });

    return () => {
      cancelled = true;
    };
  }, [gameId, setDocuments]);

  const handleUpload = useCallback(
    async (file: File, onProgress: (percent: number) => void) => {
      if (!gameId) throw new Error('Nessun gioco selezionato');
      return api.pdf.uploadPdf(gameId, file, onProgress);
    },
    [gameId]
  );

  const handleUploadComplete = useCallback(
    (documentId: string, fileName: string, fileSizeBytes: number) => {
      setUploadedDocId(documentId);
      setCustomPdfUploaded(true);
      setShowUpload(false);

      // Add to existing docs list
      setExistingDocs(prev => [
        ...prev,
        {
          id: documentId,
          gameId: gameId ?? '',
          fileName,
          filePath: '',
          fileSizeBytes,
          processingStatus: 'Processing',
          uploadedAt: new Date().toISOString(),
          processedAt: null,
          pageCount: null,
          documentType: 'base' as const,
          isPublic: false,
          processingState: 'Uploading',
          progressPercentage: 0,
          retryCount: 0,
          maxRetries: 3,
          canRetry: false,
          errorCategory: null,
          processingError: null,
          documentCategory: 'Rulebook',
          baseDocumentId: null,
          isActiveForRag: true,
          hasAcceptedDisclaimer: false,
          versionLabel: null,
        },
      ]);
    },
    [gameId, setCustomPdfUploaded]
  );

  const hasDocuments = existingDocs.length > 0;
  const showEmbeddingStatus = uploadedDocId && gameId;

  return (
    <div className="space-y-5" data-testid="knowledge-base-step">
      {/* Header */}
      <div>
        <div className="flex items-center gap-2 mb-1.5">
          <BookOpen className="h-5 w-5 text-teal-400" />
          <h3 className="text-base font-semibold text-slate-100">Knowledge Base</h3>
        </div>
        <p className="text-sm text-slate-400">
          I PDF del regolamento vengono usati dall&apos;AI per rispondere alle tue domande sul
          gioco.
          {!hasDocuments && !loadingDocs && ' Puoi caricare un PDF o saltare questo passaggio.'}
        </p>
      </div>

      {/* Existing PDFs */}
      {(hasDocuments || loadingDocs) && (
        <div>
          <h4 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
            PDF disponibili
          </h4>
          <PdfList documents={existingDocs} loading={loadingDocs} />
        </div>
      )}

      {/* Embedding status for recently uploaded PDF */}
      {showEmbeddingStatus && (
        <div data-testid="kb-embedding-status">
          <RagReadyIndicator
            gameId={gameId}
            gameName={selectedGame?.title}
            enabled
            className="border-slate-700 bg-slate-800/50"
          />
        </div>
      )}

      {/* Upload section */}
      <div>
        {!showUpload && !customPdfUploaded ? (
          <button
            type="button"
            onClick={() => setShowUpload(true)}
            className={cn(
              'flex w-full items-center gap-3 rounded-lg border border-dashed border-slate-700 px-4 py-3',
              'text-left transition-colors hover:border-teal-500/50 hover:bg-slate-800/30'
            )}
            data-testid="show-upload-button"
          >
            <Upload className="h-5 w-5 text-slate-500" />
            <div>
              <p className="text-sm font-medium text-slate-300">Carica un PDF personalizzato</p>
              <p className="text-xs text-slate-500">Aggiungi il regolamento o un manuale privato</p>
            </div>
          </button>
        ) : customPdfUploaded && !showUpload ? (
          <div
            className="flex items-center gap-2 rounded-lg border border-teal-500/30 bg-teal-500/5 px-4 py-3 text-sm text-teal-400"
            data-testid="upload-success"
          >
            <BookOpen className="h-4 w-4" />
            PDF caricato con successo
          </div>
        ) : null}

        {showUpload && (
          <PdfUploadZone onUpload={handleUpload} onUploadComplete={handleUploadComplete} />
        )}
      </div>

      {/* Info: step is optional */}
      <p className="text-xs text-slate-500 text-center" data-testid="skip-info">
        Questo passaggio è opzionale. Puoi procedere senza caricare PDF.
      </p>
    </div>
  );
}
