'use client';

/**
 * AiToolkitGenerator — AI-powered toolkit generation component (Issue P0-8).
 *
 * Three states:
 *  1. Initial: "Generate with AI" trigger button
 *  2. Loading: Spinner with "Analyzing game rules..." message
 *  3. Review:  Card showing the suggestion with Apply / Dismiss / Regenerate
 *
 * The caller controls isGenerating / isApplying; this component manages
 * suggestion and error state internally.
 */

import React, { useState } from 'react';

import { Bot, Check, Loader2, Sparkles, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import type { AiToolkitSuggestion } from '@/lib/api/schemas/toolkit.schemas';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface AiToolkitGeneratorProps {
  gameId: string;
  onGenerate: (gameId: string) => Promise<AiToolkitSuggestion>;
  onApply: (suggestion: AiToolkitSuggestion) => Promise<void>;
  onDismiss: () => void;
  isGenerating?: boolean;
  isApplying?: boolean;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function AiToolkitGenerator({
  gameId,
  onGenerate,
  onApply,
  onDismiss,
  isGenerating = false,
  isApplying = false,
}: AiToolkitGeneratorProps) {
  const [suggestion, setSuggestion] = useState<AiToolkitSuggestion | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleGenerate = async () => {
    setError(null);
    setSuggestion(null);
    try {
      const result = await onGenerate(gameId);
      setSuggestion(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore nella generazione AI');
    }
  };

  const handleApply = async () => {
    if (!suggestion) return;
    setError(null);
    try {
      await onApply(suggestion);
      setSuggestion(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Errore nell'applicazione del suggerimento");
    }
  };

  const handleDismiss = () => {
    setSuggestion(null);
    setError(null);
    onDismiss();
  };

  const busy = isGenerating || isApplying;

  // ── State 1: Initial — trigger button ──────────────────────────────────────

  if (!suggestion && !isGenerating) {
    return (
      <div className="space-y-2">
        <Button
          variant="outline"
          onClick={() => void handleGenerate()}
          disabled={busy}
          className="w-full border-amber-300 dark:border-amber-700 text-amber-700 dark:text-amber-300 hover:bg-amber-50 dark:hover:bg-amber-900/20"
        >
          <Sparkles className="w-4 h-4 mr-2" />
          Genera con AI
        </Button>
        {error && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}
      </div>
    );
  }

  // ── State 2: Loading ───────────────────────────────────────────────────────

  if (isGenerating) {
    return (
      <Card className="border-amber-300 dark:border-amber-700 bg-amber-50/50 dark:bg-amber-900/10">
        <CardContent className="flex items-center justify-center gap-3 py-6">
          <Loader2 className="w-5 h-5 animate-spin text-amber-600 dark:text-amber-400" />
          <span className="text-sm text-amber-700 dark:text-amber-300 font-medium">
            Analisi regole del gioco in corso...
          </span>
        </CardContent>
      </Card>
    );
  }

  // ── State 3: Review ────────────────────────────────────────────────────────

  if (!suggestion) return null;

  const diceCount = suggestion.diceTools.length;
  const counterCount = suggestion.counterTools.length;
  const timerCount = suggestion.timerTools.length;
  const hasScoringTemplate = suggestion.scoringTemplate !== null;
  const hasTurnTemplate = suggestion.turnTemplate !== null;

  return (
    <Card className="border-amber-300 dark:border-amber-700 bg-amber-50/50 dark:bg-amber-900/10">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Bot className="w-5 h-5 text-amber-600 dark:text-amber-400" />
          <span>Suggerimento AI: {suggestion.toolkitName}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Tool count badges */}
        <div className="flex flex-wrap gap-2">
          {diceCount > 0 && (
            <Badge variant="secondary" className="text-xs">
              {diceCount} {diceCount === 1 ? 'dado' : 'dadi'}
            </Badge>
          )}
          {counterCount > 0 && (
            <Badge variant="secondary" className="text-xs">
              {counterCount} {counterCount === 1 ? 'contatore' : 'contatori'}
            </Badge>
          )}
          {timerCount > 0 && (
            <Badge variant="secondary" className="text-xs">
              {timerCount} {timerCount === 1 ? 'timer' : 'timer'}
            </Badge>
          )}
          {hasScoringTemplate && (
            <Badge variant="secondary" className="text-xs">
              Punteggio
            </Badge>
          )}
          {hasTurnTemplate && (
            <Badge variant="secondary" className="text-xs">
              Turni
            </Badge>
          )}
        </div>

        {/* Reasoning */}
        {suggestion.reasoning && (
          <p className="text-sm italic text-stone-500 dark:text-stone-400">
            {suggestion.reasoning}
          </p>
        )}

        {/* Counter details */}
        {counterCount > 0 && (
          <div className="space-y-1">
            <p className="text-xs font-medium text-stone-600 dark:text-stone-300">
              Dettagli contatori:
            </p>
            <ul className="text-xs text-stone-500 dark:text-stone-400 space-y-0.5 pl-4 list-disc">
              {suggestion.counterTools.map(c => (
                <li key={c.name}>
                  <span className="font-medium">{c.name}</span> ({c.minValue}–{c.maxValue}, default{' '}
                  {c.defaultValue}
                  {c.isPerPlayer ? ', per giocatore' : ''})
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Error */}
        {error && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}

        {/* Action buttons */}
        <div className="flex items-center gap-2 pt-1">
          <Button
            size="sm"
            onClick={() => void handleApply()}
            disabled={busy}
            className="bg-amber-600 hover:bg-amber-700 text-white"
          >
            {isApplying ? (
              <Loader2 className="w-3 h-3 animate-spin mr-1" />
            ) : (
              <Check className="w-3 h-3 mr-1" />
            )}
            Applica
          </Button>
          <Button size="sm" variant="ghost" onClick={handleDismiss} disabled={busy}>
            <X className="w-3 h-3 mr-1" />
            Ignora
          </Button>
          <Button size="sm" variant="outline" onClick={() => void handleGenerate()} disabled={busy}>
            <Sparkles className="w-3 h-3 mr-1" />
            Rigenera
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
