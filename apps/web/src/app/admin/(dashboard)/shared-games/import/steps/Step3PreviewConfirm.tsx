'use client';

/**
 * Step 3: Preview & Confirm
 *
 * - Read-only MeepleCard preview (featured variant)
 * - Summary of all metadata
 * - "Crea gioco" CTA (triggers goNext → Step 4 runs saga)
 */

import type { JSX } from 'react';

import { Calendar, Clock, Users } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useMetadataToMeepleCard } from '@/hooks/useMetadataToMeepleCard';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

export function Step3PreviewConfirm(): JSX.Element {
  const { reviewedMetadata, coverImage } = useGameImportWizardStore();

  const meepleCardProps = useMetadataToMeepleCard(reviewedMetadata, coverImage);

  const meta = reviewedMetadata;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* ── MeepleCard Preview ── */}
        <div className="space-y-3">
          <h3 className="text-base font-semibold">Anteprima scheda</h3>
          <div className="flex justify-center">
            <div className="w-64">
              <MeepleCard {...meepleCardProps} />
            </div>
          </div>
        </div>

        {/* ── Metadata Summary ── */}
        <div className="space-y-4">
          <h3 className="text-base font-semibold">Riepilogo metadati</h3>

          {meta ? (
            <dl className="space-y-2 text-sm">
              <div className="flex gap-2">
                <dt className="w-36 shrink-0 font-medium text-muted-foreground">Titolo</dt>
                <dd className="font-semibold">{meta.title}</dd>
              </div>

              {meta.yearPublished != null && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">
                    <span className="inline-flex items-center gap-1">
                      <Calendar className="h-3.5 w-3.5" /> Anno
                    </span>
                  </dt>
                  <dd>{meta.yearPublished}</dd>
                </div>
              )}

              {(meta.minPlayers != null || meta.maxPlayers != null) && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">
                    <span className="inline-flex items-center gap-1">
                      <Users className="h-3.5 w-3.5" /> Giocatori
                    </span>
                  </dt>
                  <dd>
                    {meta.minPlayers != null && meta.maxPlayers != null
                      ? `${meta.minPlayers}–${meta.maxPlayers}`
                      : (meta.minPlayers ?? meta.maxPlayers)}
                  </dd>
                </div>
              )}

              {meta.playingTimeMinutes != null && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">
                    <span className="inline-flex items-center gap-1">
                      <Clock className="h-3.5 w-3.5" /> Durata
                    </span>
                  </dt>
                  <dd>{meta.playingTimeMinutes} min</dd>
                </div>
              )}

              {meta.minAge != null && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Età minima</dt>
                  <dd>{meta.minAge}+</dd>
                </div>
              )}

              {meta.publishers && meta.publishers.length > 0 && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Editori</dt>
                  <dd>{meta.publishers.join(', ')}</dd>
                </div>
              )}

              {meta.designers && meta.designers.length > 0 && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Autori</dt>
                  <dd>{meta.designers.join(', ')}</dd>
                </div>
              )}

              {meta.categories && meta.categories.length > 0 && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Categorie</dt>
                  <dd>{meta.categories.join(', ')}</dd>
                </div>
              )}

              {meta.mechanics && meta.mechanics.length > 0 && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Meccaniche</dt>
                  <dd>{meta.mechanics.join(', ')}</dd>
                </div>
              )}

              {meta.description && (
                <div className="flex gap-2">
                  <dt className="w-36 shrink-0 font-medium text-muted-foreground">Descrizione</dt>
                  <dd className="text-muted-foreground">{meta.description}</dd>
                </div>
              )}

              {/* Cover image summary */}
              <div className="flex gap-2">
                <dt className="w-36 shrink-0 font-medium text-muted-foreground">Copertina</dt>
                <dd>
                  {coverImage.mode === 'placeholder' && 'Placeholder colore entità'}
                  {coverImage.mode === 'pdf-page' &&
                    `Pagina PDF${coverImage.pdfPageNumber != null ? ` ${coverImage.pdfPageNumber}` : ''}`}
                  {coverImage.mode === 'upload' && 'Immagine caricata'}
                </dd>
              </div>
            </dl>
          ) : (
            <p className="text-sm text-muted-foreground">Nessun metadato disponibile.</p>
          )}

          <p className="mt-4 text-xs text-muted-foreground">
            Clicca <strong>Avanti</strong> per creare il gioco nel catalogo condiviso e avviare
            l&apos;indicizzazione RAG del regolamento.
          </p>
        </div>
      </div>
    </div>
  );
}
