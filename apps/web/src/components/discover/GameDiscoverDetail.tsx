'use client';

import { useEffect, useState } from 'react';

import type { RulebookAnalysisDto, SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';
import { useGameTitle } from '@/lib/i18n/use-game-title';

import { AnalysisResultsPanel } from './AnalysisResultsPanel';
import { GameDiscoverHero } from './GameDiscoverHero';
import { GameRulesSection } from './GameRulesSection';

interface GameDiscoverDetailProps {
  game: SharedGameDetail;
}

export function GameDiscoverDetail({ game }: GameDiscoverDetailProps) {
  const [analyses, setAnalyses] = useState<RulebookAnalysisDto[]>([]);

  // Issue #2339 — viewer-locale title resolution. `GameRulesSection` consumes
  // the resolved title as plain-text body copy ("Regole non ancora
  // disponibili per X"); the canonical EN remains accessible via the Hero
  // aria-label fallback per DEC-FE-9.
  const { value: title } = useGameTitle(game);

  useEffect(() => {
    fetch(`/api/v1/shared-games/${game.id}/analysis`)
      .then(r => (r.ok ? r.json() : []))
      .then((data: RulebookAnalysisDto[]) => setAnalyses(data))
      .catch(() => setAnalyses([]));
  }, [game.id]);

  return (
    <div className="space-y-8">
      <GameDiscoverHero game={game} />
      {analyses.length > 0 && <AnalysisResultsPanel analyses={analyses} />}
      <GameRulesSection rules={game.rules} gameTitle={title} />
    </div>
  );
}
