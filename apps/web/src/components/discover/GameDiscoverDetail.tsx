'use client';

import type { SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

import { GameDiscoverHero } from './GameDiscoverHero';
import { GameRulesSection } from './GameRulesSection';

interface GameDiscoverDetailProps {
  game: SharedGameDetail;
}

export function GameDiscoverDetail({ game }: GameDiscoverDetailProps) {
  return (
    <div className="space-y-8">
      <GameDiscoverHero game={game} />
      <GameRulesSection rules={game.rules} bggId={game.bggId} gameTitle={game.title} />
    </div>
  );
}
