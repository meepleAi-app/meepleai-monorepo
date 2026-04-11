'use client';

import { useState, useEffect } from 'react';

import { BookOpen, Trophy, Zap, Layers } from 'lucide-react';

import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';
import { getCachedAnalyses, cacheRulebookAnalyses } from '@/lib/game-night/rules-cache';

interface RulesContentProps {
  gameId: string | null;
}

export function RulesContent({ gameId }: RulesContentProps) {
  const [analysis, setAnalysis] = useState<RulebookAnalysisDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  useEffect(() => {
    if (!gameId) return;

    // Try cache first
    const cached = getCachedAnalyses(gameId);
    if (cached && cached.analyses.length > 0) {
      setAnalysis(cached.analyses[0]);
      return;
    }

    // Fetch from API
    setLoading(true);
    setError(false);

    const client = createSharedGamesClient({ httpClient: new HttpClient() });
    client
      .getGameAnalysis(gameId)
      .then(analyses => {
        if (analyses.length > 0) {
          cacheRulebookAnalyses(gameId, analyses);
          setAnalysis(analyses[0]);
        }
      })
      .catch(() => setError(true))
      .finally(() => setLoading(false));
  }, [gameId]);

  if (!gameId) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div data-testid="rules-loading" className="flex items-center justify-center py-8">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      </div>
    );
  }

  if (error || !analysis) {
    return (
      <div data-testid="rules-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <BookOpen className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Regole non disponibili per questo gioco.</p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="rules-content" className="space-y-4 text-sm">
      {/* Summary */}
      <section>
        <p className="text-muted-foreground leading-relaxed">{analysis.summary}</p>
      </section>

      {/* Victory conditions */}
      {analysis.victoryConditions && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Trophy className="h-3.5 w-3.5 text-amber-500" />
            <span>Come si vince</span>
          </div>
          <p className="text-muted-foreground">{analysis.victoryConditions.primary}</p>
        </section>
      )}

      {/* Key mechanics */}
      {analysis.keyMechanics.length > 0 && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Zap className="h-3.5 w-3.5 text-blue-500" />
            <span>Meccaniche</span>
          </div>
          <div className="flex flex-wrap gap-1">
            {analysis.keyMechanics.map(m => (
              <span
                key={m}
                className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs"
              >
                {m}
              </span>
            ))}
          </div>
        </section>
      )}

      {/* Game phases */}
      {analysis.gamePhases.length > 0 && (
        <section>
          <div className="flex items-center gap-1.5 mb-1.5 font-semibold text-foreground">
            <Layers className="h-3.5 w-3.5 text-purple-500" />
            <span>Fasi di gioco</span>
          </div>
          <ol className="space-y-1">
            {analysis.gamePhases
              .sort((a, b) => a.order - b.order)
              .map(phase => (
                <li key={phase.name} className="text-muted-foreground">
                  <span className="font-medium text-foreground">{phase.name}</span>
                  {' — '}
                  {phase.description}
                  {phase.isOptional && (
                    <span className="ml-1 text-xs text-muted-foreground">(opzionale)</span>
                  )}
                </li>
              ))}
          </ol>
        </section>
      )}
    </div>
  );
}
