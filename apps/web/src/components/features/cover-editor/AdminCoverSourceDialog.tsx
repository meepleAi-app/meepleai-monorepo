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
import { useSetManualCover } from '@/hooks/admin/useSetManualCover';
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

// The DEC-3c whitelist the backend accepts (LicenseValidator). Constraining the select to these
// keeps the copyright gate from being tripped by a typo; the server still re-validates (400).
const MANUAL_LICENSES = ['CC0', 'CC-BY-4.0', 'CC-BY-SA-4.0', 'Public domain'] as const;

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
  const setManual = useSetManualCover();

  const [activeContext, setActiveContext] = useState<CoverContext>('Card');
  const [pendingSource, setPendingSource] = useState<CoverAssignmentSource | null>(null);
  const [focal, setFocal] = useState(DEFAULT_FOCAL);
  const [manualUrl, setManualUrl] = useState('');
  const [manualLicense, setManualLicense] = useState<string>(MANUAL_LICENSES[0]);
  const [manualAttribution, setManualAttribution] = useState('');

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

  // Clear a staged (un-applied) grid pick ONLY when the admin switches context — deliberately
  // NOT on a candidates refetch. Adding a Manual source below reloads `data` (new candidate),
  // which must not discard an in-progress grid selection: the two are independent concerns.
  useEffect(() => {
    setPendingSource(null);
  }, [activeContext]);

  // Pre-fill the focal from the active context's persisted assignment whenever it (re)loads, so
  // the picker opens on the saved value and "dirty" is a genuine diff against that baseline.
  useEffect(() => {
    const assignment = data?.assignments[ASSIGN_KEY[activeContext]] ?? null;
    setFocal(assignment ? { x: assignment.focalX, y: assignment.focalY } : DEFAULT_FOCAL);
  }, [activeContext, data]);

  // Reset ALL edit state when the dialog closes so nothing resurfaces on the next open — the
  // grid pick + focal AND the manual-URL form fields + its mutation status (a stale error alert
  // or a half-typed URL must not reappear). `reset` is a stable react-query callback.
  const resetManual = setManual.reset;
  useEffect(() => {
    if (!open) {
      setActiveContext('Card');
      setPendingSource(null);
      setFocal(DEFAULT_FOCAL);
      setManualUrl('');
      setManualAttribution('');
      setManualLicense(MANUAL_LICENSES[0]);
      resetManual();
    }
  }, [open, resetManual]);

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

  const handleAddManual = () => {
    const url = manualUrl.trim();
    if (!url) return;
    setManual.mutate(
      {
        gameId,
        body: {
          sourceUrl: url,
          license: manualLicense,
          attribution: manualAttribution.trim() || null,
        },
      },
      {
        onSuccess: () => {
          // The new Manual candidate arrives via the candidates refetch; clear the inputs so
          // the form is ready for a re-try/replace without a stale URL lingering.
          setManualUrl('');
          setManualAttribution('');
        },
      }
    );
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

        {data && (
          <section
            aria-labelledby="manual-cover-heading"
            className="space-y-2 border-t border-border pt-4"
          >
            <h3 id="manual-cover-heading" className="text-sm font-medium text-foreground">
              Aggiungi copertina da URL
            </h3>
            <p className="text-xs text-muted-foreground">
              Immagine con licenza libera (dominio pubblico / CC0 / CC-BY / CC-BY-SA); l&apos;URL
              deve essere HTTPS. Dopo il caricamento la sorgente &laquo;Manuale&raquo; diventa
              selezionabile qui sopra.
            </p>
            <div className="grid gap-2 sm:grid-cols-2">
              <label className="flex flex-col gap-1 text-xs text-muted-foreground sm:col-span-2">
                URL immagine
                <input
                  type="url"
                  value={manualUrl}
                  onChange={e => setManualUrl(e.target.value)}
                  placeholder="https://…"
                  className="rounded border border-border bg-background px-2 py-1.5 text-sm text-foreground"
                />
              </label>
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                Licenza
                <select
                  value={manualLicense}
                  onChange={e => setManualLicense(e.target.value)}
                  className="rounded border border-border bg-background px-2 py-1.5 text-sm text-foreground"
                >
                  {MANUAL_LICENSES.map(l => (
                    <option key={l} value={l}>
                      {l}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                Attribuzione (facoltativa)
                <input
                  type="text"
                  value={manualAttribution}
                  onChange={e => setManualAttribution(e.target.value)}
                  className="rounded border border-border bg-background px-2 py-1.5 text-sm text-foreground"
                />
              </label>
            </div>
            {setManual.isError && (
              <p role="alert" className="text-xs text-destructive">
                Impossibile aggiungere la copertina: verifica che l&apos;URL sia HTTPS e che la
                licenza sia consentita.
              </p>
            )}
            <Button
              variant="outline"
              onClick={handleAddManual}
              disabled={!manualUrl.trim() || setManual.isPending}
            >
              {setManual.isPending ? 'Caricamento…' : 'Carica da URL'}
            </Button>
          </section>
        )}
      </DialogContent>
    </Dialog>
  );
}
