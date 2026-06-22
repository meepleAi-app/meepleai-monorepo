/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

/**
 * PdfVersionManager Component
 * Task 6: PDF Version Manager with replace/keep flow
 *
 * Lists PDFs for a game, allows uploading new versions with labels and
 * categories, and offers "Replace or keep both?" when an existing PDF is
 * already present.
 */

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { FileText, Plus, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { PdfUploadZone } from '@/components/library/add-game-sheet/steps/PdfUploadZone';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface PdfVersionManagerProps {
  gameId: string;
  gameName: string;
}

type DocumentCategory = 'Rulebook' | 'Expansion' | 'Errata' | 'Reference';

const DOCUMENT_CATEGORIES: DocumentCategory[] = ['Rulebook', 'Expansion', 'Errata', 'Reference'];

const CATEGORY_LABELS: Record<DocumentCategory, string> = {
  Rulebook: 'Regolamento',
  Expansion: 'Espansione',
  Errata: 'Errata',
  Reference: 'Riferimento',
};

// ============================================================================
// Sub-components
// ============================================================================

/** Badge for document category with semantic color coding */
function CategoryBadge({ category }: { category: string }) {
  const colorMap: Record<string, string> = {
    Rulebook: 'bg-teal-500/15 text-teal-300 border-teal-500/30',
    Expansion: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
    Errata: 'bg-red-500/15 text-red-300 border-red-500/30',
    Reference: 'bg-blue-500/15 text-blue-300 border-blue-500/30',
  };

  const colorClass = colorMap[category] ?? 'bg-muted-foreground/15 text-slate-300 border-border/30';

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium',
        colorClass
      )}
    >
      {CATEGORY_LABELS[category as DocumentCategory] ?? category}
    </span>
  );
}

/** Processing state badge */
function StateBadge({ state }: { state: string }) {
  const isReady = state === 'Ready' || state === 'Completed';
  const isProcessing = ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing'].includes(
    state
  );
  const isFailed = state === 'Failed';

  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        isReady && 'bg-green-500/15 text-green-300',
        isProcessing && 'bg-blue-500/15 text-blue-300',
        isFailed && 'bg-red-500/15 text-red-300',
        !isReady && !isProcessing && !isFailed && 'bg-muted-foreground/15 text-muted-foreground'
      )}
    >
      {isProcessing && <Loader2 className="mr-1 h-3 w-3 animate-spin" />}
      {state}
    </span>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function PdfVersionManager({ gameId, gameName }: PdfVersionManagerProps) {
  const queryClient = useQueryClient();

  // UI state
  const [showUploadForm, setShowUploadForm] = useState(false);
  const [versionLabel, setVersionLabel] = useState('');
  const [category, setCategory] = useState<DocumentCategory>('Rulebook');
  const [_newDocumentId, setNewDocumentId] = useState<string | null>(null);
  const [showReplaceDialog, setShowReplaceDialog] = useState(false);

  // ─── Queries ──────────────────────────────────────────────────────────────

  const {
    data: pdfs = [],
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['game-pdfs', gameId],
    queryFn: () => api.documents.getDocumentsByGame(gameId),
  });

  // ─── Mutations ────────────────────────────────────────────────────────────

  const toggleRagMutation = useMutation({
    mutationFn: ({ pdfId, isActive }: { pdfId: string; isActive: boolean }) =>
      api.documents.setActiveForRag(pdfId, isActive),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['game-pdfs', gameId] });
    },
    onError: (err: unknown) => {
      const message = err instanceof Error ? err.message : 'Impossibile aggiornare lo stato RAG.';
      toast.error(message);
    },
  });

  // ─── Upload Handlers ──────────────────────────────────────────────────────

  // NOTE: versionLabel and category are collected in the UI but not yet persisted
  // to the backend — the upload API does not support these fields yet.
  // When the backend adds support, forward them in handleUpload/handleUploadComplete.
  const handleUpload = async (
    file: File,
    onProgress: (percent: number) => void
  ): Promise<{ documentId: string; fileName: string }> => {
    const result = await api.pdf.uploadPdf(gameId, file, onProgress);
    return { documentId: result.documentId, fileName: result.fileName };
  };

  const handleUploadComplete = (documentId: string, fileName: string) => {
    setNewDocumentId(documentId);
    const activePdfs = pdfs.filter(p => p.isActiveForRag);

    if (activePdfs.length > 0) {
      // Existing PDFs present → ask user
      setShowReplaceDialog(true);
    } else {
      // No existing PDFs → just close the form
      finishUpload();
      toast.success(`"${fileName}" caricato con successo.`);
    }
  };

  const finishUpload = () => {
    setShowUploadForm(false);
    setVersionLabel('');
    setCategory('Rulebook');
    setNewDocumentId(null);
    void queryClient.invalidateQueries({ queryKey: ['game-pdfs', gameId] });
  };

  const handleReplace = async () => {
    // Deactivate all currently active PDFs
    const activePdfs = pdfs.filter(p => p.isActiveForRag);
    await Promise.all(
      activePdfs.map(pdf => toggleRagMutation.mutateAsync({ pdfId: pdf.id, isActive: false }))
    );
    setShowReplaceDialog(false);
    finishUpload();
    toast.success('Versione sostituita con successo.');
  };

  const handleKeepBoth = () => {
    setShowReplaceDialog(false);
    finishUpload();
    toast.success('Entrambe le versioni sono ora attive.');
  };

  const handleToggleRag = (pdf: PdfDocumentDto) => {
    void toggleRagMutation.mutateAsync({ pdfId: pdf.id, isActive: !pdf.isActiveForRag });
  };

  // ─── Render ───────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div
        className="flex items-center justify-center p-8 text-muted-foreground"
        data-testid="pdf-version-manager-loading"
      >
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Caricamento PDF...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="p-4 text-center text-red-400" data-testid="pdf-version-manager-error">
        Errore nel caricamento dei PDF. Riprova.
      </div>
    );
  }

  return (
    <div className="space-y-4" data-testid="pdf-version-manager">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-200">
          Versioni PDF — <span className="text-muted-foreground">{gameName}</span>
        </h3>
        {!showUploadForm && (
          <Button
            size="sm"
            onClick={() => setShowUploadForm(true)}
            className="bg-teal-600 hover:bg-teal-700"
            data-testid="add-version-button"
          >
            <Plus className="mr-1.5 h-4 w-4" />
            Carica nuova versione
          </Button>
        )}
      </div>

      {/* Upload Form */}
      {showUploadForm && (
        <div
          className="rounded-xl border border-border bg-card p-4 space-y-4"
          data-testid="upload-form"
        >
          <h4 className="text-sm font-medium text-slate-200">Nuova versione</h4>

          {/* Version Label */}
          <div className="space-y-1.5">
            <Label htmlFor="version-label" className="text-xs text-muted-foreground">
              Etichetta versione
            </Label>
            <Input
              id="version-label"
              placeholder="es. 2a Edizione"
              value={versionLabel}
              onChange={e => setVersionLabel(e.target.value)}
              className="bg-card border-border text-slate-200 placeholder:text-muted-foreground"
              data-testid="version-label-input"
            />
          </div>

          {/* Category Select */}
          <div className="space-y-1.5">
            <Label htmlFor="document-category" className="text-xs text-muted-foreground">
              Categoria
            </Label>
            <select
              id="document-category"
              value={category}
              onChange={e => setCategory(e.target.value as DocumentCategory)}
              className="w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-2 focus:ring-teal-500/50"
              data-testid="category-select"
            >
              {DOCUMENT_CATEGORIES.map(cat => (
                <option key={cat} value={cat}>
                  {CATEGORY_LABELS[cat]}
                </option>
              ))}
            </select>
          </div>

          {/* Upload Zone */}
          <PdfUploadZone
            onUpload={handleUpload}
            onUploadComplete={handleUploadComplete}
            data-testid="upload-zone"
          />

          {/* Cancel */}
          <div className="flex justify-end">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setShowUploadForm(false);
                setVersionLabel('');
                setCategory('Rulebook');
              }}
              className="border-border text-slate-300 hover:bg-card"
            >
              Annulla
            </Button>
          </div>
        </div>
      )}

      {/* PDF List */}
      {pdfs.length === 0 && !showUploadForm ? (
        <div
          className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border py-10 text-center"
          data-testid="pdf-version-manager-empty"
        >
          <FileText className="mb-2 h-8 w-8 text-muted-foreground" />
          <p className="text-sm font-medium text-muted-foreground">Nessun regolamento caricato</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Clicca "Carica nuova versione" per aggiungere un PDF.
          </p>
        </div>
      ) : (
        pdfs.length > 0 && (
          <div className="overflow-hidden rounded-xl border border-border">
            <table className="w-full text-sm" data-testid="pdf-list-table">
              <thead>
                <tr className="border-b border-border bg-card">
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">File</th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">
                    Versione
                  </th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">
                    Categoria
                  </th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">
                    Stato
                  </th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-muted-foreground">RAG</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700/50">
                {pdfs.map(pdf => (
                  <tr key={pdf.id} className="hover:bg-card transition-colors">
                    <td className="px-4 py-3 text-slate-200">
                      <div className="flex items-center gap-2">
                        <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                        <span className="truncate max-w-[180px]" title={pdf.fileName}>
                          {pdf.fileName}
                        </span>
                      </div>
                    </td>
                    <td
                      className="px-4 py-3 text-muted-foreground"
                      data-testid={`version-label-${pdf.id}`}
                    >
                      {pdf.versionLabel ?? '—'}
                    </td>
                    <td className="px-4 py-3">
                      <CategoryBadge category={pdf.documentCategory} />
                    </td>
                    <td className="px-4 py-3">
                      <StateBadge state={pdf.processingState} />
                    </td>
                    <td className="px-4 py-3">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleToggleRag(pdf)}
                        disabled={toggleRagMutation.isPending}
                        className={cn(
                          'h-7 rounded-full px-3 text-xs font-medium transition-colors',
                          pdf.isActiveForRag
                            ? 'bg-teal-500/20 text-teal-300 hover:bg-teal-500/30'
                            : 'bg-card text-muted-foreground hover:bg-slate-600'
                        )}
                        aria-label={pdf.isActiveForRag ? 'Attivo' : 'Inattivo'}
                        data-testid={`rag-toggle-${pdf.id}`}
                      >
                        {pdf.isActiveForRag ? 'Attivo' : 'Inattivo'}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      )}

      {/* Replace or Keep Both Dialog */}
      <Dialog open={showReplaceDialog} onOpenChange={setShowReplaceDialog}>
        <DialogContent data-testid="replace-keep-dialog" className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Sostituire la versione esistente?</DialogTitle>
            <DialogDescription>
              {gameName} ha già {pdfs.filter(p => p.isActiveForRag).length} PDF attivo
              {pdfs.filter(p => p.isActiveForRag).length !== 1 && 'i'}. Vuoi sostituirlo con la
              nuova versione o tenere entrambi attivi per il RAG?
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button
              variant="outline"
              onClick={handleKeepBoth}
              className="border-border text-slate-300 hover:bg-card"
              data-testid="keep-both-button"
            >
              Tieni entrambi
            </Button>
            <Button
              onClick={() => void handleReplace()}
              className="bg-amber-600 hover:bg-amber-700 text-white"
              disabled={toggleRagMutation.isPending}
              data-testid="replace-button"
            >
              {toggleRagMutation.isPending ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : null}
              Sostituisci
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
