'use client';

/**
 * ScoreAssistant — AI-powered natural language score input for live sessions.
 *
 * Users type messages like "Marco ha 5 punti" and the AI parses, resolves
 * players, and records scores. Handles ambiguous players, low confidence,
 * and confirmation flow.
 *
 * Issue #121 — AI Score Tracking
 */

import { useState, useCallback, type FormEvent } from 'react';

import { CheckCircle2, AlertTriangle, HelpCircle, Loader2, Send, Mic, XCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type {
  ScoreParseResult,
  ConfirmScoreRequest,
} from '@/lib/api/schemas/score-tracking.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface ScoreAssistantProps {
  sessionId: string;
  /** Called after a score is successfully recorded */
  onScoreRecorded?: () => void;
  className?: string;
}

type AssistantState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'result'; data: ScoreParseResult }
  | { kind: 'error'; message: string };

// ============================================================================
// Component
// ============================================================================

export function ScoreAssistant({ sessionId, onScoreRecorded, className }: ScoreAssistantProps) {
  const [input, setInput] = useState('');
  const [state, setState] = useState<AssistantState>({ kind: 'idle' });

  // ----- Parse message -----
  const handleSubmit = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      const trimmed = input.trim();
      if (!trimmed) return;

      setState({ kind: 'loading' });

      try {
        const result = await api.liveSessions.parseScore(sessionId, {
          message: trimmed,
          autoRecord: true,
        });

        if (result.status === 'recorded') {
          onScoreRecorded?.();
        }

        setState({ kind: 'result', data: result });
        setInput('');
      } catch {
        setState({ kind: 'error', message: 'Errore nella comunicazione con il server.' });
      }
    },
    [input, sessionId, onScoreRecorded]
  );

  // ----- Confirm a parsed score -----
  const handleConfirm = useCallback(
    async (result: ScoreParseResult) => {
      if (!result.playerId || result.value == null || !result.round || !result.dimension) return;

      setState({ kind: 'loading' });

      try {
        const request: ConfirmScoreRequest = {
          playerId: result.playerId,
          dimension: result.dimension,
          value: result.value,
          round: result.round,
        };
        await api.liveSessions.confirmScore(sessionId, request);
        onScoreRecorded?.();
        setState({
          kind: 'result',
          data: {
            ...result,
            status: 'recorded',
            requiresConfirmation: false,
            message: `Punteggio registrato: ${result.playerName} ${result.value} ${result.dimension}.`,
          },
        });
      } catch {
        setState({ kind: 'error', message: 'Errore nella conferma del punteggio.' });
      }
    },
    [sessionId, onScoreRecorded]
  );

  // ----- Dismiss result -----
  const handleDismiss = useCallback(() => {
    setState({ kind: 'idle' });
  }, []);

  return (
    <section
      className={cn(
        'rounded-xl border border-slate-200 dark:border-slate-700',
        'bg-white/80 dark:bg-slate-800/60 backdrop-blur-sm p-3',
        className
      )}
      data-testid="score-assistant"
    >
      {/* Header */}
      <div className="flex items-center gap-2 mb-2">
        <Mic className="h-4 w-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
        <span className="text-sm font-semibold text-slate-900 dark:text-slate-100">
          Assistente punteggi
        </span>
      </div>

      {/* Input form */}
      <form onSubmit={handleSubmit} className="flex items-center gap-2">
        <Input
          value={input}
          onChange={e => setInput(e.target.value)}
          placeholder="Es: Marco ha 5 punti"
          disabled={state.kind === 'loading'}
          className="flex-1 h-9 text-sm"
          data-testid="score-input"
        />
        <Button
          type="submit"
          size="icon"
          disabled={state.kind === 'loading' || !input.trim()}
          className={cn(
            'h-9 w-9 flex-shrink-0',
            'bg-amber-600 hover:bg-amber-700 text-white',
            'active:scale-95 transition-all'
          )}
          data-testid="score-submit"
        >
          {state.kind === 'loading' ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <Send className="h-4 w-4" aria-hidden="true" />
          )}
          <span className="sr-only">Invia</span>
        </Button>
      </form>

      {/* Result display */}
      {state.kind === 'result' && (
        <ResultCard result={state.data} onConfirm={handleConfirm} onDismiss={handleDismiss} />
      )}

      {/* Error display */}
      {state.kind === 'error' && (
        <div
          className="mt-2 flex items-center gap-2 text-sm text-red-600 dark:text-red-400"
          data-testid="score-error"
        >
          <XCircle className="h-4 w-4 flex-shrink-0" aria-hidden="true" />
          <span>{state.message}</span>
          <Button variant="ghost" size="sm" onClick={handleDismiss} className="ml-auto h-7 text-xs">
            OK
          </Button>
        </div>
      )}
    </section>
  );
}

// ============================================================================
// Result Card
// ============================================================================

interface ResultCardProps {
  result: ScoreParseResult;
  onConfirm: (result: ScoreParseResult) => void;
  onDismiss: () => void;
}

function ResultCard({ result, onConfirm, onDismiss }: ResultCardProps) {
  const statusConfig = {
    recorded: {
      icon: CheckCircle2,
      color: 'text-emerald-600 dark:text-emerald-400',
      bg: 'bg-emerald-50 dark:bg-emerald-900/20',
    },
    parsed: {
      icon: HelpCircle,
      color: 'text-amber-600 dark:text-amber-400',
      bg: 'bg-amber-50 dark:bg-amber-900/20',
    },
    ambiguous: {
      icon: AlertTriangle,
      color: 'text-orange-600 dark:text-orange-400',
      bg: 'bg-orange-50 dark:bg-orange-900/20',
    },
    unrecognized: {
      icon: XCircle,
      color: 'text-slate-500 dark:text-slate-400',
      bg: 'bg-slate-50 dark:bg-slate-800/40',
    },
  };

  const config = statusConfig[result.status];
  const Icon = config.icon;

  return (
    <div
      className={cn('mt-2 rounded-lg p-3', config.bg)}
      data-testid="score-result"
      data-status={result.status}
    >
      <div className="flex items-start gap-2">
        <Icon className={cn('h-4 w-4 mt-0.5 flex-shrink-0', config.color)} aria-hidden="true" />
        <div className="flex-1 min-w-0">
          <p className="text-sm text-slate-800 dark:text-slate-200">{result.message}</p>

          {/* Confidence indicator */}
          <div className="flex items-center gap-1 mt-1">
            <span className="text-xs text-slate-500">Confidenza:</span>
            <span
              className={cn(
                'text-xs font-medium',
                result.confidence >= 0.8
                  ? 'text-emerald-600'
                  : result.confidence >= 0.5
                    ? 'text-amber-600'
                    : 'text-red-600'
              )}
            >
              {Math.round(result.confidence * 100)}%
            </span>
          </div>

          {/* Ambiguous candidates */}
          {result.status === 'ambiguous' && result.ambiguousCandidates && (
            <div className="mt-1.5">
              <span className="text-xs text-slate-500">Giocatori possibili:</span>
              <div className="flex flex-wrap gap-1 mt-1">
                {result.ambiguousCandidates.map(name => (
                  <span
                    key={name}
                    className="inline-flex items-center rounded-full bg-white dark:bg-slate-700 px-2 py-0.5 text-xs font-medium text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-600"
                  >
                    {name}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Action buttons */}
          <div className="flex items-center gap-2 mt-2">
            {result.requiresConfirmation && result.playerId && result.value != null && (
              <Button
                size="sm"
                onClick={() => onConfirm(result)}
                className="h-7 text-xs bg-emerald-600 hover:bg-emerald-700 text-white"
                data-testid="score-confirm"
              >
                Conferma
              </Button>
            )}
            <Button
              variant="ghost"
              size="sm"
              onClick={onDismiss}
              className="h-7 text-xs"
              data-testid="score-dismiss"
            >
              {result.status === 'recorded' ? 'OK' : 'Annulla'}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
