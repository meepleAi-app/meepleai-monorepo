'use client';

import { useEffect, useState } from 'react';

import type { RulebookAnalysisDto, SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

import { AnalysisResultsPanel } from './AnalysisResultsPanel';
import { GameDiscoverHero } from './GameDiscoverHero';
import { GameRulesSection } from './GameRulesSection';

interface GameDiscoverDetailProps {
  game: SharedGameDetail;
}

export function GameDiscoverDetail({ game }: GameDiscoverDetailProps) {
  const [analyses, setAnalyses] = useState<RulebookAnalysisDto[]>([]);

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
      <GameRulesSection rules={game.rules} bggId={game.bggId} gameTitle={game.title} />
    </div>
  );
}
