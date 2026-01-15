/**
 * Player Mode Controls - Issue #2421
 *
 * UI controls for Player Mode AI suggestions.
 *
 * Features:
 * - "Suggest Move" button with tooltip
 * - AI suggestion display panel
 * - Confidence meter visualization
 * - Apply Move / Ignore actions
 * - Loading and error states
 * - Alternative moves display
 */

'use client';

import { useState } from 'react';

import { Brain, Check, Lightbulb, Sparkles, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { usePlayerAISuggestion } from '@/lib/hooks/usePlayerAISuggestion';

// ========== Component Props ==========

export interface PlayerModeControlsProps {
  /** Game ID for AI context */
  gameId: string;
  /** Current game state object */
  gameState: Record<string, any>;
  /** Optional query/context for AI */
  query?: string;
  /** Callback when suggestion is applied */
  onSuggestionApplied?: (suggestion: { action: string; rationale: string }) => void;
  /** Callback when suggestion is ignored */
  onSuggestionIgnored?: () => void;
  /** Read-only mode (disables all interactions) */
  readonly?: boolean;
}

// ========== Confidence Meter Component ==========

interface ConfidenceMeterProps {
  confidence: number; // 0.0 - 1.0
}

function ConfidenceMeter({ confidence }: ConfidenceMeterProps) {
  const percentage = Math.round(confidence * 100);
  const colorClass =
    confidence >= 0.8 ? 'bg-green-500' : confidence >= 0.5 ? 'bg-yellow-500' : 'bg-red-500';

  const label = confidence >= 0.8 ? 'Alto' : confidence >= 0.5 ? 'Medio' : 'Basso';

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium text-muted-foreground">Confidenza</span>
        <span className="font-semibold">
          {label} ({percentage}%)
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full transition-all duration-500 ${colorClass}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
}

// ========== Main Component ==========

export function PlayerModeControls({
  gameId,
  gameState,
  query,
  onSuggestionApplied,
  onSuggestionIgnored,
  readonly = false,
}: PlayerModeControlsProps) {
  const [userQuery, setUserQuery] = useState(query || '');

  const [suggestionState, suggestionControls] = usePlayerAISuggestion({
    onSuggestionReceived: (suggestion, confidence) => {
      console.log('Received suggestion:', suggestion, 'Confidence:', confidence);
    },
    onSuggestionApplied: suggestion => {
      if (onSuggestionApplied) {
        onSuggestionApplied({
          action: suggestion.action,
          rationale: suggestion.rationale,
        });
      }
    },
    onSuggestionIgnored: () => {
      if (onSuggestionIgnored) {
        onSuggestionIgnored();
      }
    },
    onError: error => {
      console.error('Suggestion error:', error);
    },
  });

  const handleSuggestMove = async () => {
    await suggestionControls.suggestMove(gameId, gameState, userQuery || undefined);
  };

  const handleApply = () => {
    suggestionControls.applySuggestion();
  };

  const handleIgnore = () => {
    suggestionControls.ignoreSuggestion();
  };

  return (
    <div className="space-y-4">
      {/* Suggest Move Button */}
      <div className="flex items-center gap-4">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                onClick={handleSuggestMove}
                disabled={readonly || suggestionState.isLoading}
                variant="default"
                size="lg"
                className="gap-2"
              >
                <Sparkles className="h-5 w-5" />
                {suggestionState.isLoading ? 'Analizzando...' : 'Suggerisci Mossa'}
              </Button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="max-w-xs">
              <p>
                L&apos;AI analizzerà lo stato attuale del gioco e suggerirà la mossa migliore da
                effettuare.
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>

        {suggestionState.processingTimeMs && !suggestionState.isLoading && (
          <span className="text-sm text-muted-foreground">
            {suggestionState.processingTimeMs}ms
          </span>
        )}
      </div>

      {/* Error Display */}
      {suggestionState.error && (
        <Card className="border-destructive">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-destructive">
              <X className="h-5 w-5" />
              Errore
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">{suggestionState.error}</p>
          </CardContent>
        </Card>
      )}

      {/* Suggestion Display */}
      {suggestionState.suggestion && !suggestionState.isLoading && (
        <Card className="border-primary/20 bg-primary/5">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Brain className="h-5 w-5 text-primary" />
              Suggerimento AI
            </CardTitle>
            <CardDescription>
              Mossa consigliata basata sull&apos;analisi dello stato
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Confidence Meter */}
            {suggestionState.confidence !== null && (
              <ConfidenceMeter confidence={suggestionState.confidence} />
            )}

            {/* Primary Suggestion */}
            <div className="space-y-2">
              <h4 className="flex items-center gap-2 font-semibold">
                <Lightbulb className="h-4 w-4" />
                Azione Suggerita
              </h4>
              <p className="text-sm font-medium">{suggestionState.suggestion.action}</p>
              <p className="text-sm text-muted-foreground">
                {suggestionState.suggestion.rationale}
              </p>
              {suggestionState.suggestion.expectedOutcome && (
                <div className="rounded-md border border-muted bg-muted/30 p-3">
                  <p className="text-xs font-medium text-muted-foreground">Risultato Atteso:</p>
                  <p className="text-sm">{suggestionState.suggestion.expectedOutcome}</p>
                </div>
              )}
            </div>

            {/* Strategic Context */}
            {suggestionState.strategicContext && (
              <div className="rounded-md border border-accent bg-accent/10 p-3">
                <p className="text-xs font-medium text-muted-foreground">Contesto Strategico:</p>
                <p className="text-sm">{suggestionState.strategicContext}</p>
              </div>
            )}

            {/* Alternative Moves */}
            {suggestionState.alternatives.length > 0 && (
              <div className="space-y-2">
                <h4 className="text-sm font-semibold text-muted-foreground">Mosse Alternative</h4>
                <div className="space-y-2">
                  {suggestionState.alternatives.map((alt, idx) => (
                    <div key={idx} className="rounded-md border border-muted bg-muted/20 p-2">
                      <p className="text-xs font-medium">{alt.action}</p>
                      <p className="text-xs text-muted-foreground">{alt.rationale}</p>
                      <span className="text-xs text-muted-foreground">
                        Confidenza: {Math.round(alt.confidence * 100)}%
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Actions */}
            <div className="flex gap-2">
              <Button
                onClick={handleApply}
                disabled={readonly}
                variant="default"
                className="flex-1 gap-2"
              >
                <Check className="h-4 w-4" />
                Applica Mossa
              </Button>
              <Button
                onClick={handleIgnore}
                disabled={readonly}
                variant="outline"
                className="gap-2"
              >
                <X className="h-4 w-4" />
                Ignora
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* No Suggestion State */}
      {!suggestionState.suggestion && !suggestionState.isLoading && !suggestionState.error && (
        <Card className="border-dashed">
          <CardHeader>
            <CardDescription className="text-center">
              Clicca &quot;Suggerisci Mossa&quot; per ricevere un suggerimento dall&apos;AI
            </CardDescription>
          </CardHeader>
        </Card>
      )}
    </div>
  );
}
