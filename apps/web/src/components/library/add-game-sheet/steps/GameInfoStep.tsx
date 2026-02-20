'use client';

/**
 * GameInfoStep - Step 3: Review/edit game info and save to collection.
 * Issue #4821: Step 3 Info & Save
 * Epic #4817: User Collection Wizard
 */

import { useState, useCallback, useMemo } from 'react';
import { Save, Loader2, AlertCircle, Tag } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';
import { api } from '@/lib/api';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

import { SuccessState } from './SuccessState';

interface FieldErrors {
  title?: string;
  minPlayers?: string;
  maxPlayers?: string;
  playingTimeMinutes?: string;
  complexityRating?: string;
  yearPublished?: string;
  description?: string;
}

export interface GameInfoStepProps {
  /** Called after successful save with libraryEntryId */
  onSuccess?: (libraryEntryId: string) => void;
  /** Called to reset wizard for adding another game */
  onAddAnother: () => void;
  /** Called to auto-close the drawer */
  onClose: () => void;
}

const SOURCE_LABELS: Record<string, string> = {
  catalog: 'Catalogo',
  bgg: 'BGG',
  custom: 'Custom',
};

export function GameInfoStep({ onSuccess, onAddAnother, onClose }: GameInfoStepProps) {
  const selectedGame = useAddGameWizardStore((s) => s.selectedGame);
  const setGameInfo = useAddGameWizardStore((s) => s.setGameInfo);

  // Form state - pre-filled from selectedGame
  const [title, setTitle] = useState(selectedGame?.title ?? '');
  const [minPlayers, setMinPlayers] = useState(String(selectedGame?.minPlayers ?? ''));
  const [maxPlayers, setMaxPlayers] = useState(String(selectedGame?.maxPlayers ?? ''));
  const [playingTime, setPlayingTime] = useState(
    selectedGame?.playingTimeMinutes ? String(selectedGame.playingTimeMinutes) : '',
  );
  const [complexity, setComplexity] = useState(
    selectedGame?.complexityRating ? String(selectedGame.complexityRating) : '',
  );
  const [yearPublished, setYearPublished] = useState(
    selectedGame?.yearPublished ? String(selectedGame.yearPublished) : '',
  );
  const [description, setDescription] = useState(selectedGame?.description ?? '');

  // Save state
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [savedEntryId, setSavedEntryId] = useState<string | null>(null);

  const isReadOnly = selectedGame?.source === 'catalog' || selectedGame?.source === 'bgg';
  const sourceLabel = SOURCE_LABELS[selectedGame?.source ?? ''] ?? '';
  const currentYear = new Date().getFullYear();

  const categories = useMemo(() => selectedGame?.categories ?? [], [selectedGame?.categories]);
  const mechanics = useMemo(() => selectedGame?.mechanics ?? [], [selectedGame?.mechanics]);

  const validate = useCallback((): boolean => {
    const errs: FieldErrors = {};

    if (!title.trim()) {
      errs.title = 'Il nome è obbligatorio';
    } else if (title.length > 200) {
      errs.title = 'Max 200 caratteri';
    }

    const min = parseInt(minPlayers);
    if (!minPlayers || isNaN(min) || min < 1 || min > 99) {
      errs.minPlayers = 'Min 1, Max 99';
    }

    const max = parseInt(maxPlayers);
    if (!maxPlayers || isNaN(max) || max < 1 || max > 99) {
      errs.maxPlayers = 'Min 1, Max 99';
    } else if (!isNaN(min) && max < min) {
      errs.maxPlayers = 'Deve essere ≥ min giocatori';
    }

    if (playingTime) {
      const time = parseInt(playingTime);
      if (isNaN(time) || time < 1 || time > 10000) {
        errs.playingTimeMinutes = 'Min 1, Max 10000 minuti';
      }
    }

    if (complexity) {
      const c = parseFloat(complexity);
      if (isNaN(c) || c < 1 || c > 5) {
        errs.complexityRating = 'Da 1.0 a 5.0';
      }
    }

    if (yearPublished) {
      const y = parseInt(yearPublished);
      if (isNaN(y) || y < 1900 || y > currentYear + 2) {
        errs.yearPublished = `Da 1900 a ${currentYear + 2}`;
      }
    }

    if (description && description.length > 5000) {
      errs.description = 'Max 5000 caratteri';
    }

    setFieldErrors(errs);
    return Object.keys(errs).length === 0;
  }, [title, minPlayers, maxPlayers, playingTime, complexity, yearPublished, description, currentYear]);

  const handleSave = useCallback(async () => {
    if (!selectedGame) return;
    if (!validate()) return;

    setSaving(true);
    setError(null);

    try {
      // Save form values to the store
      const infoValues = {
        title: title.trim(),
        minPlayers: parseInt(minPlayers),
        maxPlayers: parseInt(maxPlayers),
        playingTimeMinutes: playingTime ? parseInt(playingTime) : undefined,
        complexityRating: complexity ? parseFloat(complexity) : undefined,
        yearPublished: yearPublished ? parseInt(yearPublished) : undefined,
        description: description.trim() || undefined,
      };
      setGameInfo(infoValues);

      // For custom source, update private game metadata first
      if (selectedGame.source === 'custom') {
        await api.library.updatePrivateGame(selectedGame.gameId, {
          title: infoValues.title,
          minPlayers: infoValues.minPlayers,
          maxPlayers: infoValues.maxPlayers,
          playingTimeMinutes: infoValues.playingTimeMinutes ?? null,
          complexityRating: infoValues.complexityRating ?? null,
          yearPublished: infoValues.yearPublished ?? null,
          description: infoValues.description ?? null,
        });
      }

      // Add game to library (works for all sources)
      const entry = await api.library.addGame(selectedGame.gameId);
      setSavedEntryId(entry.id);
      onSuccess?.(entry.id);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore durante il salvataggio';
      setError(message);
    } finally {
      setSaving(false);
    }
  }, [
    selectedGame,
    validate,
    title,
    minPlayers,
    maxPlayers,
    playingTime,
    complexity,
    yearPublished,
    description,
    setGameInfo,
    onSuccess,
  ]);

  // Show success state after save
  if (savedEntryId) {
    return (
      <SuccessState
        gameTitle={title}
        libraryEntryId={savedEntryId}
        gameId={selectedGame?.gameId}
        onAddAnother={onAddAnother}
        onAutoClose={onClose}
      />
    );
  }

  return (
    <div className="space-y-5" data-testid="game-info-step">
      {/* Header */}
      <div>
        <h3 className="text-base font-semibold text-slate-100 mb-1">
          Rivedi informazioni
        </h3>
        <p className="text-sm text-slate-400">
          {isReadOnly
            ? 'Conferma i dati del gioco dal catalogo.'
            : 'Modifica i campi se necessario, poi salva.'}
        </p>
      </div>

      {/* Image preview + source */}
      {selectedGame?.imageUrl && (
        <div className="flex items-center gap-3">
          <img
            src={selectedGame.thumbnailUrl ?? selectedGame.imageUrl}
            alt={selectedGame.title}
            className="h-16 w-16 rounded-lg object-cover bg-slate-800"
          />
          <div>
            <p className="text-sm font-medium text-slate-200">{selectedGame.title}</p>
            {sourceLabel && (
              <span className="inline-flex items-center gap-1 rounded-full bg-slate-700/50 px-2 py-0.5 text-[10px] font-medium text-slate-400 mt-1">
                Fonte: {sourceLabel}
              </span>
            )}
          </div>
        </div>
      )}

      {/* Form fields */}
      <div className="space-y-3.5">
        {/* Title */}
        <div className="space-y-1.5">
          <Label htmlFor="info-title" className="text-xs text-slate-400">
            Nome gioco *
          </Label>
          <Input
            id="info-title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Nome del gioco"
            className="bg-slate-800 border-slate-700"
            disabled={saving || isReadOnly}
            data-testid="info-title"
          />
          {fieldErrors.title && <p className="text-xs text-red-400">{fieldErrors.title}</p>}
        </div>

        {/* Players Row */}
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="info-min-players" className="text-xs text-slate-400">
              Min giocatori *
            </Label>
            <Input
              id="info-min-players"
              type="number"
              min={1}
              max={99}
              value={minPlayers}
              onChange={(e) => setMinPlayers(e.target.value)}
              placeholder="1"
              className="bg-slate-800 border-slate-700"
              disabled={saving || isReadOnly}
              data-testid="info-min-players"
            />
            {fieldErrors.minPlayers && (
              <p className="text-xs text-red-400">{fieldErrors.minPlayers}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="info-max-players" className="text-xs text-slate-400">
              Max giocatori *
            </Label>
            <Input
              id="info-max-players"
              type="number"
              min={1}
              max={99}
              value={maxPlayers}
              onChange={(e) => setMaxPlayers(e.target.value)}
              placeholder="4"
              className="bg-slate-800 border-slate-700"
              disabled={saving || isReadOnly}
              data-testid="info-max-players"
            />
            {fieldErrors.maxPlayers && (
              <p className="text-xs text-red-400">{fieldErrors.maxPlayers}</p>
            )}
          </div>
        </div>

        {/* Playing time + Complexity */}
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="info-time" className="text-xs text-slate-400">
              Tempo (min)
            </Label>
            <Input
              id="info-time"
              type="number"
              min={1}
              max={10000}
              value={playingTime}
              onChange={(e) => setPlayingTime(e.target.value)}
              placeholder="60"
              className="bg-slate-800 border-slate-700"
              disabled={saving || isReadOnly}
              data-testid="info-time"
            />
            {fieldErrors.playingTimeMinutes && (
              <p className="text-xs text-red-400">{fieldErrors.playingTimeMinutes}</p>
            )}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="info-complexity" className="text-xs text-slate-400">
              Complessità (1-5)
            </Label>
            <Input
              id="info-complexity"
              type="number"
              min={1}
              max={5}
              step={0.1}
              value={complexity}
              onChange={(e) => setComplexity(e.target.value)}
              placeholder="3.0"
              className="bg-slate-800 border-slate-700"
              disabled={saving || isReadOnly}
              data-testid="info-complexity"
            />
            {fieldErrors.complexityRating && (
              <p className="text-xs text-red-400">{fieldErrors.complexityRating}</p>
            )}
          </div>
        </div>

        {/* Year */}
        <div className="space-y-1.5">
          <Label htmlFor="info-year" className="text-xs text-slate-400">
            Anno pubblicazione
          </Label>
          <Input
            id="info-year"
            type="number"
            min={1900}
            max={currentYear + 2}
            value={yearPublished}
            onChange={(e) => setYearPublished(e.target.value)}
            placeholder={String(currentYear)}
            className="bg-slate-800 border-slate-700"
            disabled={saving || isReadOnly}
            data-testid="info-year"
          />
          {fieldErrors.yearPublished && (
            <p className="text-xs text-red-400">{fieldErrors.yearPublished}</p>
          )}
        </div>

        {/* Description */}
        <div className="space-y-1.5">
          <Label htmlFor="info-description" className="text-xs text-slate-400">
            Descrizione
          </Label>
          <Textarea
            id="info-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Descrizione del gioco..."
            rows={3}
            className="bg-slate-800 border-slate-700 resize-none"
            disabled={saving || isReadOnly}
            data-testid="info-description"
          />
          {fieldErrors.description && (
            <p className="text-xs text-red-400">{fieldErrors.description}</p>
          )}
        </div>
      </div>

      {/* Categories & Mechanics */}
      {(categories.length > 0 || mechanics.length > 0) && (
        <div className="space-y-2.5">
          {categories.length > 0 && (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1.5">
                Categorie
              </p>
              <div className="flex flex-wrap gap-1.5" data-testid="categories-tags">
                {categories.map((cat) => (
                  <span
                    key={cat}
                    className="inline-flex items-center gap-1 rounded-full bg-slate-700/50 px-2.5 py-0.5 text-xs text-slate-300"
                  >
                    <Tag className="h-2.5 w-2.5" />
                    {cat}
                  </span>
                ))}
              </div>
            </div>
          )}
          {mechanics.length > 0 && (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1.5">
                Meccaniche
              </p>
              <div className="flex flex-wrap gap-1.5" data-testid="mechanics-tags">
                {mechanics.map((mech) => (
                  <span
                    key={mech}
                    className="inline-flex items-center gap-1 rounded-full bg-slate-700/50 px-2.5 py-0.5 text-xs text-slate-300"
                  >
                    {mech}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Error */}
      {error && (
        <div
          className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400"
          role="alert"
          data-testid="save-error"
        >
          <AlertCircle className="h-3.5 w-3.5 shrink-0" />
          {error}
        </div>
      )}

      {/* Save Button */}
      <Button
        onClick={handleSave}
        disabled={saving}
        className="w-full bg-teal-600 hover:bg-teal-700 gap-2"
        data-testid="save-button"
      >
        {saving ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <Save className="h-4 w-4" />
        )}
        {saving ? 'Salvataggio...' : 'Salva in Collezione'}
      </Button>
    </div>
  );
}
