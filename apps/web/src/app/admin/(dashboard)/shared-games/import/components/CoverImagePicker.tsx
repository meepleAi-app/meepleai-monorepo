'use client';

/**
 * CoverImagePicker — 3-tab cover image selector for the PDF import wizard.
 *
 * Tab 1: Placeholder (default, entity color)
 * Tab 2: Extract from PDF page
 * Tab 3: Upload custom image (PNG/JPG, max 5 MB)
 */

import { type ChangeEvent, useCallback, useState } from 'react';
import type { JSX } from 'react';

import { ImageIcon, FileText, Upload, CheckCircle, AlertCircle } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { CoverImageSelection } from '@/stores/useGameImportWizardStore';

export interface CoverImagePickerProps {
  pdfDocumentId: string | null;
  value: CoverImageSelection;
  onChange: (selection: CoverImageSelection) => void;
}

const MAX_IMAGE_SIZE = 5 * 1024 * 1024; // 5 MB

type TabKey = 'placeholder' | 'pdf-page' | 'upload';

interface TabConfig {
  key: TabKey;
  label: string;
  icon: React.ReactNode;
}

const TABS: TabConfig[] = [
  { key: 'placeholder', label: 'Nessuna', icon: <ImageIcon className="h-4 w-4" /> },
  { key: 'pdf-page', label: 'Dal PDF', icon: <FileText className="h-4 w-4" /> },
  { key: 'upload', label: 'Carica', icon: <Upload className="h-4 w-4" /> },
];

export function CoverImagePicker({
  pdfDocumentId,
  value,
  onChange,
}: CoverImagePickerProps): JSX.Element {
  const activeTab = value.mode;
  const [pageNumber, setPageNumber] = useState(1);
  const [pdfPreviewUrl, setPdfPreviewUrl] = useState<string | null>(null);
  const [loadingPage, setLoadingPage] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const selectTab = useCallback(
    (tab: TabKey) => {
      if (tab === 'placeholder') {
        onChange({ mode: 'placeholder', imageUrl: null });
        setPdfPreviewUrl(null);
      } else if (tab === 'pdf-page') {
        onChange({ mode: 'pdf-page', imageUrl: null });
      } else {
        onChange({ mode: 'upload', imageUrl: null });
      }
    },
    [onChange]
  );

  // Load a page image from the PDF
  const loadPdfPage = useCallback(
    async (page: number) => {
      if (!pdfDocumentId) {
        setPageError('PDF non ancora caricato');
        return;
      }
      setLoadingPage(true);
      setPageError(null);
      try {
        const url = api.sharedGames.getPdfPageImageUrl(pdfDocumentId, page);
        // Fetch to validate response (CORS + auth)
        const res = await fetch(url, { credentials: 'include' });
        if (!res.ok) throw new Error(`Errore HTTP ${res.status}`);
        const blob = await res.blob();
        const objectUrl = URL.createObjectURL(blob);
        setPdfPreviewUrl(objectUrl);
        onChange({ mode: 'pdf-page', imageUrl: objectUrl, pdfPageNumber: page });
      } catch (e) {
        setPageError(e instanceof Error ? e.message : 'Impossibile caricare la pagina');
        setPdfPreviewUrl(null);
      } finally {
        setLoadingPage(false);
      }
    },
    [pdfDocumentId, onChange]
  );

  const handlePageChange = useCallback((e: ChangeEvent<HTMLInputElement>) => {
    const val = parseInt(e.target.value);
    if (!isNaN(val) && val > 0) setPageNumber(val);
  }, []);

  // Handle custom image upload
  const handleImageUpload = useCallback(
    async (e: ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      setUploadError(null);

      if (!['image/png', 'image/jpeg'].includes(file.type)) {
        setUploadError('Formato non supportato. Usa PNG o JPG.');
        return;
      }
      if (file.size > MAX_IMAGE_SIZE) {
        setUploadError(
          `Immagine troppo grande (max 5 MB, attuale ${(file.size / 1024 / 1024).toFixed(1)} MB)`
        );
        return;
      }

      const objectUrl = URL.createObjectURL(file);
      onChange({ mode: 'upload', imageUrl: objectUrl });
    },
    [onChange]
  );

  return (
    <div className="space-y-3">
      {/* Tab buttons */}
      <div className="flex gap-1 rounded-lg border bg-muted/30 p-1">
        {TABS.map(tab => (
          <button
            key={tab.key}
            onClick={() => selectTab(tab.key)}
            className={`flex flex-1 items-center justify-center gap-1.5 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
              activeTab === tab.key
                ? 'bg-background text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab: Placeholder */}
      {activeTab === 'placeholder' && (
        <div className="flex items-center gap-3 rounded-md border bg-muted/30 p-4">
          <div className="flex h-16 w-12 items-center justify-center rounded bg-orange-500/20 text-orange-600">
            <ImageIcon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm font-medium">Placeholder colore entità</p>
            <p className="text-xs text-muted-foreground">
              Verrà usato il colore predefinito per i giochi (arancione)
            </p>
          </div>
          <CheckCircle className="ml-auto h-4 w-4 text-green-500" />
        </div>
      )}

      {/* Tab: PDF Page */}
      {activeTab === 'pdf-page' && (
        <div className="space-y-3">
          <div className="flex items-end gap-2">
            <div className="flex-1 space-y-1">
              <Label htmlFor="pdf-page-number">Numero pagina</Label>
              <Input
                id="pdf-page-number"
                type="number"
                min={1}
                value={pageNumber}
                onChange={handlePageChange}
                className="w-24"
              />
            </div>
            <Button
              size="sm"
              variant="outline"
              onClick={() => loadPdfPage(pageNumber)}
              disabled={loadingPage || !pdfDocumentId}
            >
              {loadingPage ? 'Caricamento...' : 'Carica anteprima'}
            </Button>
          </div>

          {pageError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription className="text-sm">{pageError}</AlertDescription>
            </Alert>
          )}

          {pdfPreviewUrl && (
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">Anteprima pagina {pageNumber}</p>
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={pdfPreviewUrl}
                alt={`Pagina ${pageNumber} del PDF`}
                className="max-h-48 rounded border object-contain"
              />
              <p className="flex items-center gap-1 text-xs text-green-600">
                <CheckCircle className="h-3 w-3" /> Pagina selezionata come copertina
              </p>
            </div>
          )}

          {!pdfPreviewUrl && !loadingPage && (
            <p className="text-xs text-muted-foreground">
              Inserisci il numero di pagina e clicca &quot;Carica anteprima&quot; per selezionare
              una copertina dal PDF.
            </p>
          )}
        </div>
      )}

      {/* Tab: Custom upload */}
      {activeTab === 'upload' && (
        <div className="space-y-3">
          <div className="space-y-1">
            <Label htmlFor="cover-image-upload">Carica immagine (PNG/JPG, max 5 MB)</Label>
            <Input
              id="cover-image-upload"
              type="file"
              accept="image/png,image/jpeg"
              onChange={handleImageUpload}
              className="cursor-pointer file:mr-4 file:cursor-pointer file:rounded-md file:border-0 file:bg-primary file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-primary-foreground hover:file:bg-primary/90"
            />
          </div>

          {uploadError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription className="text-sm">{uploadError}</AlertDescription>
            </Alert>
          )}

          {value.imageUrl && value.mode === 'upload' && (
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">Anteprima</p>
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={value.imageUrl}
                alt="Copertina caricata"
                className="max-h-48 rounded border object-contain"
              />
              <p className="flex items-center gap-1 text-xs text-green-600">
                <CheckCircle className="h-3 w-3" /> Immagine selezionata
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
