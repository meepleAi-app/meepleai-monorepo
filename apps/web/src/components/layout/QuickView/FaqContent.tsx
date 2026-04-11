'use client';

import { useState, useEffect } from 'react';

import { HelpCircle } from 'lucide-react';

import { createSharedGamesClient } from '@/lib/api/clients/sharedGamesClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { RulebookAnalysisDto } from '@/lib/api/schemas/shared-games.schemas';
import { getCachedAnalyses, cacheRulebookAnalyses } from '@/lib/game-night/rules-cache';

interface FaqContentProps {
  gameId: string | null;
}

export function FaqContent({ gameId }: FaqContentProps) {
  const [analysis, setAnalysis] = useState<RulebookAnalysisDto | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!gameId) return;

    const cached = getCachedAnalyses(gameId);
    if (cached && cached.analyses.length > 0) {
      setAnalysis(cached.analyses[0]);
      return;
    }

    setLoading(true);

    const client = createSharedGamesClient({ httpClient: new HttpClient() });
    client
      .getGameAnalysis(gameId)
      .then(analyses => {
        if (analyses.length > 0) {
          cacheRulebookAnalyses(gameId, analyses);
          setAnalysis(analyses[0]);
        }
      })
      .catch(() => {
        // leave analysis null → empty state
      })
      .finally(() => setLoading(false));
  }, [gameId]);

  if (!gameId) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le FAQ</p>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div data-testid="faq-loading" className="flex items-center justify-center py-8">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      </div>
    );
  }

  const faqs = analysis?.generatedFaqs ?? [];

  if (faqs.length === 0) {
    return (
      <div data-testid="faq-content" className="flex flex-col h-full">
        <div className="flex flex-col items-center justify-center py-8 text-center">
          <HelpCircle className="h-8 w-8 text-muted-foreground/40 mb-3" />
          <p className="text-sm text-muted-foreground">FAQ non disponibili per questo gioco.</p>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="faq-content" className="space-y-3 text-sm">
      {faqs.map((faq, i) => (
        <div key={i} className="space-y-1">
          <p className="font-medium text-foreground">{faq.question}</p>
          <p className="text-muted-foreground leading-relaxed">{faq.answer}</p>
        </div>
      ))}
    </div>
  );
}
