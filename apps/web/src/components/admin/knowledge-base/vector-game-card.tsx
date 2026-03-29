/**
 * VectorGameCard Component
 * Issue #4861: pgvector migration — game-centric vector health display
 *
 * Displays a game's vector index health using MeepleCard entity="kb" variant="grid"
 * with vector count, health percentage, and failed count indicator.
 */

'use client';

import { AlertTriangle, Database } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { VectorGameBreakdown } from '@/lib/api/schemas/admin-knowledge-base.schemas';

export interface VectorGameCardProps {
  game: VectorGameBreakdown;
}

function healthLabel(healthPercent: number): string {
  if (healthPercent >= 90) return 'Healthy';
  if (healthPercent >= 70) return 'Degraded';
  return 'Error';
}

function healthVariant(healthPercent: number): 'success' | 'warning' | 'error' {
  if (healthPercent >= 90) return 'success';
  if (healthPercent >= 70) return 'warning';
  return 'error';
}

export function VectorGameCard({ game }: VectorGameCardProps) {
  const metadata: MeepleCardMetadata[] = [
    { icon: Database, label: `${game.vectorCount.toLocaleString()} vectors` },
    ...(game.failedCount > 0 ? [{ icon: AlertTriangle, label: `${game.failedCount} failed` }] : []),
  ];

  return (
    <MeepleCard
      entity="kb"
      variant="grid"
      title={game.gameName}
      subtitle={`${game.vectorCount.toLocaleString()} vectors · ${game.healthPercent.toFixed(0)}% health`}
      stateLabel={{
        text: healthLabel(game.healthPercent),
        variant: healthVariant(game.healthPercent),
      }}
      metadata={metadata}
      data-testid={`vector-game-card-${game.gameId}`}
    />
  );
}
