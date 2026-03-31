/**
 * Admin — Game Phase Templates Configuration
 *
 * Game Session Flow v2.0 — Task 13
 *
 * Allows admins to manage phase templates for a game:
 * - Add / remove / reorder phases
 * - AI-suggest phases based on game rules
 * - Save templates used as defaults in the session wizard
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

import { Plus, Trash2, Wand2, ArrowUp, ArrowDown, Save, Layers } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { PhaseTemplateDto, PhaseTemplateSuggestionDto } from '@/lib/api/clients/gamesClient';
import { cn } from '@/lib/utils';

// ========== Types ==========

interface PhaseRow {
  /** local id for React key (may be tempId for new rows) */
  localId: string;
  phaseName: string;
  description: string;
}

// ========== Helpers ==========

function toRows(templates: PhaseTemplateDto[]): PhaseRow[] {
  return [...templates]
    .sort((a, b) => a.phaseOrder - b.phaseOrder)
    .map(t => ({ localId: t.id, phaseName: t.phaseName, description: t.description ?? '' }));
}

// ========== Page ==========

export default function GamePhasesAdminPage({ params }: { params: { id: string } }) {
  const router = useRouter();
  const gameId = params.id;

  const [rows, setRows] = useState<PhaseRow[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSuggesting, setIsSuggesting] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  // ── Load existing templates ────────────────────────────────────────────────

  useEffect(() => {
    setIsLoading(true);
    api.games
      .getPhaseTemplates(gameId)
      .then(templates => setRows(toRows(templates)))
      .catch(() => setError('Errore nel caricamento delle fasi'))
      .finally(() => setIsLoading(false));
  }, [gameId]);

  // ── Row mutations ──────────────────────────────────────────────────────────

  const addRow = useCallback(() => {
    setRows(prev => [...prev, { localId: `new-${Date.now()}`, phaseName: '', description: '' }]);
  }, []);

  const removeRow = useCallback((localId: string) => {
    setRows(prev => prev.filter(r => r.localId !== localId));
  }, []);

  const updateRow = useCallback(
    (localId: string, field: 'phaseName' | 'description', value: string) => {
      setRows(prev => prev.map(r => (r.localId === localId ? { ...r, [field]: value } : r)));
    },
    []
  );

  const moveRow = useCallback((localId: string, dir: 'up' | 'down') => {
    setRows(prev => {
      const idx = prev.findIndex(r => r.localId === localId);
      if (idx < 0) return prev;
      const next = [...prev];
      const swapIdx = dir === 'up' ? idx - 1 : idx + 1;
      if (swapIdx < 0 || swapIdx >= next.length) return prev;
      [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
      return next;
    });
  }, []);

  // ── AI Suggestions ─────────────────────────────────────────────────────────

  const handleSuggest = useCallback(async () => {
    setIsSuggesting(true);
    setError(null);
    try {
      const suggestions: PhaseTemplateSuggestionDto[] =
        await api.games.suggestPhaseTemplates(gameId);
      if (suggestions.length === 0) {
        setError('Nessun suggerimento disponibile. Carica prima le regole del gioco.');
        return;
      }
      setRows(
        suggestions.map(s => ({
          localId: `suggest-${s.phaseOrder}-${Date.now()}`,
          phaseName: s.phaseName,
          description: s.description,
        }))
      );
    } catch {
      setError('Errore durante la generazione dei suggerimenti AI. Riprova.');
    } finally {
      setIsSuggesting(false);
    }
  }, [gameId]);

  // ── Save ──────────────────────────────────────────────────────────────────

  const handleSave = useCallback(async () => {
    const valid = rows.filter(r => r.phaseName.trim().length > 0);
    if (valid.length === 0) {
      setError('Aggiungi almeno una fase con un nome.');
      return;
    }
    setIsSaving(true);
    setError(null);
    setSaveSuccess(false);
    try {
      await api.games.upsertPhaseTemplates(
        gameId,
        valid.map((r, i) => ({
          phaseName: r.phaseName.trim(),
          phaseOrder: i + 1,
          description: r.description.trim() || undefined,
        }))
      );
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch {
      setError('Errore nel salvataggio. Riprova.');
    } finally {
      setIsSaving(false);
    }
  }, [gameId, rows]);

  // ── Render ─────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[40vh]">
        <p className="text-sm text-muted-foreground">Caricamento fasi...</p>
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <Layers className="h-5 w-5 text-purple-400" />
            <h1 className="text-xl font-bold font-quicksand">Configurazione fasi</h1>
          </div>
          <p className="text-sm text-muted-foreground">
            Definisci le fasi del turno per questo gioco. Vengono pre-caricate nel wizard di
            sessione.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => router.back()}>
          Indietro
        </Button>
      </div>

      {/* AI Suggest */}
      <div className="rounded-xl border border-purple-500/30 bg-purple-500/5 p-4 flex items-center gap-4">
        <div className="flex-1">
          <p className="text-sm font-medium">Suggerisci fasi con AI</p>
          <p className="text-xs text-muted-foreground">
            Analizza le regole caricate e suggerisce le fasi tipiche del turno.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={handleSuggest}
          disabled={isSuggesting}
          className="border-purple-500/40 text-purple-300 hover:bg-purple-500/10 shrink-0"
        >
          <Wand2 className={cn('h-4 w-4 mr-1.5', isSuggesting && 'animate-spin')} />
          {isSuggesting ? 'Generazione...' : 'Suggerisci'}
        </Button>
      </div>

      {/* Phase list */}
      <div className="space-y-2">
        {rows.length === 0 && (
          <div className="flex items-center justify-center py-10 rounded-xl border border-dashed border-white/20">
            <p className="text-sm text-muted-foreground">
              Nessuna fase configurata. Aggiungine una o usa AI.
            </p>
          </div>
        )}
        {rows.map((row, idx) => (
          <div
            key={row.localId}
            className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 p-3"
          >
            {/* Order */}
            <span className="text-xs font-mono text-muted-foreground w-5 text-center shrink-0">
              {idx + 1}
            </span>

            {/* Inputs */}
            <div className="flex-1 flex flex-col sm:flex-row gap-2">
              <Input
                value={row.phaseName}
                onChange={e => updateRow(row.localId, 'phaseName', e.target.value)}
                placeholder="Nome fase"
                className="flex-1"
                aria-label={`Nome fase ${idx + 1}`}
              />
              <Input
                value={row.description}
                onChange={e => updateRow(row.localId, 'description', e.target.value)}
                placeholder="Descrizione (opzionale)"
                className="flex-1 text-sm"
                aria-label={`Descrizione fase ${idx + 1}`}
              />
            </div>

            {/* Move up/down */}
            <div className="flex flex-col gap-0.5 shrink-0">
              <button
                onClick={() => moveRow(row.localId, 'up')}
                disabled={idx === 0}
                className="p-1 rounded hover:bg-white/10 disabled:opacity-30"
                aria-label="Sposta su"
              >
                <ArrowUp className="h-3 w-3" />
              </button>
              <button
                onClick={() => moveRow(row.localId, 'down')}
                disabled={idx === rows.length - 1}
                className="p-1 rounded hover:bg-white/10 disabled:opacity-30"
                aria-label="Sposta giù"
              >
                <ArrowDown className="h-3 w-3" />
              </button>
            </div>

            {/* Remove */}
            <button
              onClick={() => removeRow(row.localId)}
              className="p-1.5 rounded hover:bg-red-500/10 text-muted-foreground hover:text-red-400 shrink-0"
              aria-label={`Rimuovi fase ${idx + 1}`}
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>

      {/* Add row */}
      <Button variant="outline" size="sm" onClick={addRow} className="w-full">
        <Plus className="h-4 w-4 mr-1.5" />
        Aggiungi fase
      </Button>

      {/* Error / success */}
      {error && (
        <p className="text-sm text-red-400 text-center" role="alert">
          {error}
        </p>
      )}
      {saveSuccess && (
        <p className="text-sm text-green-400 text-center" role="status">
          Fasi salvate con successo!
        </p>
      )}

      {/* Save */}
      <GradientButton fullWidth size="lg" onClick={handleSave} disabled={isSaving}>
        <Save className="h-4 w-4 mr-2" />
        {isSaving ? 'Salvataggio...' : 'Salva fasi'}
      </GradientButton>
    </div>
  );
}
