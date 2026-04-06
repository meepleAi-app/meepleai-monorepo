'use client';

/**
 * Step 2: Metadata Review + Cover Image Picker + Live MeepleCard Preview
 *
 * - Spins up metadata extraction via GET /admin/shared-games/extract-metadata/{pdfId}
 * - Editable form for all metadata fields
 * - 3-tab cover image picker (placeholder / PDF page / upload)
 * - Live MeepleCard preview (debounced 300ms)
 */

import { useEffect, type JSX } from 'react';

import { AlertCircle, Loader2 } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useExtractMetadata } from '@/hooks/queries/useExtractMetadata';
import { useMetadataToMeepleCard } from '@/hooks/useMetadataToMeepleCard';
import { useGameImportWizardStore, type GameMetadata } from '@/stores/useGameImportWizardStore';

import { CoverImagePicker } from '../components/CoverImagePicker';

export function Step2MetadataReview(): JSX.Element {
  const { uploadedPdf, reviewedMetadata, coverImage, setReviewedMetadata, setCoverImage } =
    useGameImportWizardStore();

  const pdfId = uploadedPdf?.pdfDocumentId ?? null;

  const {
    data: extractedMetadata,
    isLoading,
    isError,
    error,
  } = useExtractMetadata(pdfId, {
    enabled: !!pdfId && !reviewedMetadata,
  });

  // Auto-populate from extraction when it arrives
  useEffect(() => {
    if (extractedMetadata && !reviewedMetadata) {
      setReviewedMetadata(extractedMetadata);
    }
  }, [extractedMetadata, reviewedMetadata, setReviewedMetadata]);

  const meepleCardProps = useMetadataToMeepleCard(reviewedMetadata, coverImage);

  const current = reviewedMetadata ?? extractedMetadata ?? null;

  const updateField = <K extends keyof GameMetadata>(key: K, value: GameMetadata[K]) => {
    const updated: GameMetadata = { ...(current ?? { title: '' }), [key]: value };
    setReviewedMetadata(updated);
  };

  const updateListField = (
    key: 'publishers' | 'designers' | 'categories' | 'mechanics',
    raw: string
  ) => {
    const list = raw
      .split(',')
      .map(s => s.trim())
      .filter(Boolean);
    updateField(key, list.length > 0 ? list : undefined);
  };

  return (
    <div className="space-y-6">
      {/* Loading extraction */}
      {isLoading && (
        <div className="flex items-center gap-3 rounded-md border bg-muted/30 p-4 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Estrazione metadati in corso con l&apos;IA...
        </div>
      )}

      {/* Extraction failed — empty form, user compiles manually */}
      {isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Estrazione non disponibile</AlertTitle>
          <AlertDescription className="text-sm">
            {error?.message ?? 'Impossibile estrarre i metadati. Compila manualmente il form.'}
          </AlertDescription>
        </Alert>
      )}

      {/* Confidence hint */}
      {current?.confidenceScore != null && (
        <p className="text-xs text-muted-foreground">
          Confidenza estrazione: {Math.round(current.confidenceScore * 100)}% — verifica e correggi
          se necessario.
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* ── Form ── */}
        <div className="space-y-4">
          <h3 className="text-base font-semibold">Metadati del gioco</h3>

          {/* Title */}
          <div className="space-y-1">
            <Label htmlFor="meta-title">
              Titolo <span className="text-destructive">*</span>
            </Label>
            <Input
              id="meta-title"
              placeholder="[Non estratto — compilare manualmente]"
              value={current?.title ?? ''}
              onChange={e => updateField('title', e.target.value)}
            />
          </div>

          {/* Year */}
          <div className="space-y-1">
            <Label htmlFor="meta-year">Anno di pubblicazione</Label>
            <Input
              id="meta-year"
              type="number"
              min={1900}
              max={new Date().getFullYear() + 2}
              placeholder="es. 2020"
              value={current?.yearPublished ?? ''}
              onChange={e =>
                updateField('yearPublished', e.target.value ? Number(e.target.value) : undefined)
              }
            />
          </div>

          {/* Players */}
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <Label htmlFor="meta-min-players">Min giocatori</Label>
              <Input
                id="meta-min-players"
                type="number"
                min={1}
                placeholder="1"
                value={current?.minPlayers ?? ''}
                onChange={e =>
                  updateField('minPlayers', e.target.value ? Number(e.target.value) : undefined)
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="meta-max-players">Max giocatori</Label>
              <Input
                id="meta-max-players"
                type="number"
                min={1}
                placeholder="4"
                value={current?.maxPlayers ?? ''}
                onChange={e =>
                  updateField('maxPlayers', e.target.value ? Number(e.target.value) : undefined)
                }
              />
            </div>
          </div>

          {/* Playing time + min age */}
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <Label htmlFor="meta-time">Durata (min)</Label>
              <Input
                id="meta-time"
                type="number"
                min={1}
                placeholder="60"
                value={current?.playingTimeMinutes ?? ''}
                onChange={e =>
                  updateField(
                    'playingTimeMinutes',
                    e.target.value ? Number(e.target.value) : undefined
                  )
                }
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="meta-min-age">Età minima</Label>
              <Input
                id="meta-min-age"
                type="number"
                min={0}
                placeholder="10"
                value={current?.minAge ?? ''}
                onChange={e =>
                  updateField('minAge', e.target.value ? Number(e.target.value) : undefined)
                }
              />
            </div>
          </div>

          {/* Publishers */}
          <div className="space-y-1">
            <Label htmlFor="meta-publishers">Editori (separati da virgola)</Label>
            <Input
              id="meta-publishers"
              placeholder="es. Cranio Creations, Ghenos Games"
              value={current?.publishers?.join(', ') ?? ''}
              onChange={e => updateListField('publishers', e.target.value)}
            />
          </div>

          {/* Designers */}
          <div className="space-y-1">
            <Label htmlFor="meta-designers">Autori (separati da virgola)</Label>
            <Input
              id="meta-designers"
              placeholder="es. Uwe Rosenberg"
              value={current?.designers?.join(', ') ?? ''}
              onChange={e => updateListField('designers', e.target.value)}
            />
          </div>

          {/* Categories */}
          <div className="space-y-1">
            <Label htmlFor="meta-categories">Categorie (separati da virgola)</Label>
            <Input
              id="meta-categories"
              placeholder="es. Strategia, Fantasy"
              value={current?.categories?.join(', ') ?? ''}
              onChange={e => updateListField('categories', e.target.value)}
            />
          </div>

          {/* Mechanics */}
          <div className="space-y-1">
            <Label htmlFor="meta-mechanics">Meccaniche (separati da virgola)</Label>
            <Input
              id="meta-mechanics"
              placeholder="es. Worker Placement, Deck Building"
              value={current?.mechanics?.join(', ') ?? ''}
              onChange={e => updateListField('mechanics', e.target.value)}
            />
          </div>

          {/* Description */}
          <div className="space-y-1">
            <Label htmlFor="meta-description">Descrizione</Label>
            <Textarea
              id="meta-description"
              rows={4}
              placeholder="Descrizione del gioco..."
              value={current?.description ?? ''}
              onChange={e => updateField('description', e.target.value || undefined)}
            />
          </div>

          {/* Cover image picker */}
          <div className="space-y-1">
            <Label>Immagine di copertina</Label>
            <CoverImagePicker pdfDocumentId={pdfId} value={coverImage} onChange={setCoverImage} />
          </div>
        </div>

        {/* ── Live MeepleCard Preview ── */}
        <div className="space-y-3">
          <h3 className="text-base font-semibold">Anteprima scheda</h3>
          <p className="text-xs text-muted-foreground">
            La MeepleCard si aggiorna in tempo reale mentre compili i metadati.
          </p>
          <div className="flex justify-center">
            <div className="w-64">
              <MeepleCard {...meepleCardProps} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
