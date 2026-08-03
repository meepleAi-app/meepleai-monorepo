/**
 * AdminCoverSourceDialog (Epic #3470 — Slice 1d-c).
 *
 * The admin cover-source picker overlay (D3: no new route). Per-context tabs
 * (Card/Hero/Social); each context lists the materialized source candidates as
 * selectable thumbnails with a provenance + license chip, a focal-point control,
 * and Apply / reset-to-implicit actions. Renders ONLY presigned previewUrl images
 * (never a BGG host — #2123). Data/mutations come from the react-query hooks.
 */

'use client';

import { useEffect, useState } from 'react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useAssignCover } from '@/hooks/admin/useAssignCover';
import { useCoverCandidates } from '@/hooks/admin/useCoverCandidates';
import { useRemoveCoverAssignment } from '@/hooks/admin/useRemoveCoverAssignment';
import type {
  CoverAssignmentSource,
  CoverContext,
} from '@/lib/api/schemas/admin/admin-cover.schemas';

import { CoverFocalPointPicker } from './CoverFocalPointPicker';

export interface AdminCoverSourceDialogProps {
  gameId: string;
  title?: string;
  open: boolean;
  onClose: () => void;
}

const CONTEXTS: readonly { value: CoverContext; label: string }[] = [
  { value: 'Card', label: 'Card' },
  { value: 'Hero', label: 'Hero' },
  { value: 'Social', label: 'Social' },
];

const SOURCE_LABEL: Record<CoverAssignmentSource, string> = {
  Pdf: 'PDF',
  Bgg: 'BGG',
  Wikidata: 'Wikidata',
  Manual: 'Manuale',
};

const ASSIGN_KEY: Record<CoverContext, 'card' | 'hero' | 'social'> = {
  Card: 'card',
  Hero: 'hero',
  Social: 'social',
};

const DEFAULT_FOCAL = { x: 0.5, y: 0.5 };

export function AdminCoverSourceDialog({
  gameId,
  title,
  open,
  onClose,
}: AdminCoverSourceDialogProps): React.JSX.Element {
  // Gate the fetch on `open` so a mounted-but-closed dialog does not query.
  const { data, isLoading, isError } = useCoverCandidates(open ? gameId : '');
  const assign = useAssignCover();
  const remove = useRemoveCoverAssignment();

  const [activeContext, setActiveContext] = useState<CoverContext>('Card');
  const [pendingSource, setPendingSource] = useState<CoverAssignmentSource | null>(null);
  const [focal, setFocal] = useState(DEFAULT_FOCAL);

  const currentAssignment = data?.assignments[ASSIGN_KEY[activeContext]] ?? null;
  // Epic #3470 Slice 2e (AC-5): the read shape now carries the persisted focal, so the
  // picker pre-fills the saved value and "dirty" is a genuine diff against that baseline
  // — replacing the old focal-touched workaround.
  const baselineFocal = currentAssignment
    ? { x: currentAssignment.focalX, y: currentAssignment.focalY }
    : DEFAULT_FOCAL;
  const currentSource = currentAssignment?.source ?? null;
  const selectedSource = pendingSource ?? currentSource;
  const selectedCandidate = data?.candidates.find(c => c.source === selectedSource) ?? null;
  const dirty =
    selectedSource !== currentSource || focal.x !== baselineFocal.x || focal.y !== baselineFocal.y;

  // Sync local edit state to the active context's persisted assignment whenever the
  // context changes or the candidates reload (post-mutation): clear any pending pick and
  // pre-fill the saved focal, so the picker opens showing the current state. `data` keeps
  // a stable reference across no-op refetches (react-query structural sharing), so this
  // does not clobber an in-progress edit.
  useEffect(() => {
    const assignment = data?.assignments[ASSIGN_KEY[activeContext]] ?? null;
    setPendingSource(null);
    setFocal(assignment ? { x: assignment.focalX, y: assignment.focalY } : DEFAULT_FOCAL);
  }, [activeContext, data]);

  // Reset to the default context when the dialog closes so it never resurfaces on the
  // next open; the sync effect above re-derives the focal once data reloads.
  useEffect(() => {
    if (!open) {
      setActiveContext('Card');
      setPendingSource(null);
      setFocal(DEFAULT_FOCAL);
    }
  }, [open]);

  const handleTabChange = (value: string) => setActiveContext(value as CoverContext);

  const handlePick = (source: CoverAssignmentSource) => {
    setPendingSource(source);
    // A newly picked source has no saved focal for this context yet → center it.
    setFocal(DEFAULT_FOCAL);
  };

  const handleFocalChange = (next: { x: number; y: number }) => setFocal(next);

  const handleApply = () => {
    if (!selectedSource) return;
    assign.mutate({
      gameId,
      context: activeContext,
      body: { source: selectedSource, focalX: focal.x, focalY: focal.y },
    });
  };

  const handleReset = () => {
    remove.mutate({ gameId, context: activeContext });
    setPendingSource(null);
    setFocal(DEFAULT_FOCAL);
  };

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Copertina{title ? ` — ${title}` : ''}</DialogTitle>
          <DialogDescription>
            Scegli la sorgente e il punto focale per ciascun contesto.
          </DialogDescription>
        </DialogHeader>

        {isLoading && (
          <p role="status" className="py-6 text-center text-sm text-muted-foreground">
            Caricamento sorgenti…
          </p>
        )}
        {isError && (
          <p role="alert" className="py-6 text-center text-sm text-destructive">
            Impossibile caricare le sorgenti copertina.
          </p>
        )}

        {data && (
          <Tabs value={activeContext} onValueChange={handleTabChange}>
            <TabsList className="w-full">
              {CONTEXTS.map(c => (
                <TabsTrigger key={c.value} value={c.value} className="flex-1">
                  {c.label}
                </TabsTrigger>
              ))}
            </TabsList>

            {CONTEXTS.map(c => (
              <TabsContent key={c.value} value={c.value} className="space-y-4">
                {data.candidates.length === 0 ? (
                  <p className="py-6 text-center text-sm text-muted-foreground">
                    Nessuna sorgente copertina disponibile per questo gioco.
                  </p>
                ) : (
                  <>
                    <p className="text-sm text-muted-foreground">
                      {currentAssignment
                        ? `Attualmente: ${SOURCE_LABEL[currentAssignment.source]}`
                        : 'Attualmente: automatico (precedenza implicita)'}
                    </p>

                    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                      {data.candidates.map(cand => {
                        const isSelected = cand.source === selectedSource;
                        return (
                          <li key={cand.source}>
                            <button
                              type="button"
                              aria-pressed={isSelected}
                              aria-label={`Copertina ${SOURCE_LABEL[cand.source]}${
                                cand.license ? `, licenza ${cand.license}` : ''
                              }`}
                              onClick={() => handlePick(cand.source)}
                              className={`flex w-full flex-col overflow-hidden rounded-lg border text-left transition-colors ${
                                isSelected
                                  ? 'border-primary ring-2 ring-primary'
                                  : 'border-border hover:border-border-strong'
                              }`}
                            >
                              {/* eslint-disable-next-line @next/next/no-img-element */}
                              <img
                                src={cand.previewUrl}
                                alt={`Anteprima ${SOURCE_LABEL[cand.source]}`}
                                loading="lazy"
                                className="aspect-[2/3] w-full object-cover"
                              />
                              <span className="flex flex-wrap items-center gap-1 p-1.5">
                                <span className="rounded bg-secondary px-1.5 py-0.5 text-xs font-medium text-secondary-foreground">
                                  {SOURCE_LABEL[cand.source]}
                                </span>
                                {cand.license && (
                                  <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                                    {cand.license}
                                  </span>
                                )}
                              </span>
                            </button>
                          </li>
                        );
                      })}
                    </ul>

                    {selectedCandidate && (
                      <div className="space-y-2">
                        <p className="text-sm font-medium text-foreground">Punto focale</p>
                        <CoverFocalPointPicker
                          imageUrl={selectedCandidate.previewUrl}
                          alt={`Anteprima ${SOURCE_LABEL[selectedCandidate.source]}`}
                          x={focal.x}
                          y={focal.y}
                          onChange={handleFocalChange}
                          label="Punto focale copertina"
                        />
                      </div>
                    )}

                    <div className="flex items-center justify-between gap-2">
                      {currentAssignment ? (
                        <Button variant="outline" onClick={handleReset} disabled={remove.isPending}>
                          Reimposta ad automatico
                        </Button>
                      ) : (
                        <span />
                      )}
                      <Button
                        onClick={handleApply}
                        disabled={!selectedSource || !dirty || assign.isPending}
                      >
                        Applica
                      </Button>
                    </div>
                  </>
                )}
              </TabsContent>
            ))}
          </Tabs>
        )}
      </DialogContent>
    </Dialog>
  );
}
