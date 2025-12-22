/**
 * InfoGrid Component - Game Metadata Grid
 *
 * 3-column responsive grid displaying:
 * - Player count (Users icon)
 * - Play time (Clock icon)
 * - Difficulty/Complexity (TrendingUp icon, from BGG)
 *
 * Responsive: mobile (1-col) → tablet (2-col) → desktop (3-col)
 *
 * Issue #1841 (PAGE-005)
 */

import React from 'react';

import { Users, Clock, TrendingUp } from 'lucide-react';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';

// ============================================================================
// Types
// ============================================================================

export interface InfoGridProps {
  /** Minimum players */
  minPlayers?: number | null;
  /** Maximum players */
  maxPlayers?: number | null;
  /** Minimum play time in minutes */
  minPlayTimeMinutes?: number | null;
  /** Maximum play time in minutes */
  maxPlayTimeMinutes?: number | null;
  /** Average complexity/weight from BGG (1-5 scale) */
  averageWeight?: number | null;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Format player count range
 */
function formatPlayerCount(min?: number | null, max?: number | null): string {
  if (min === null && max === null) return 'N/A';
  if (min === max) return `${min}`;
  return `${min || '?'}–${max || '?'}`;
}

/**
 * Format play time range
 */
function formatPlayTime(min?: number | null, max?: number | null): string {
  if (min === null && max === null) return 'N/A';
  if (min === max) return `${min} min`;
  return `${min || '?'}–${max || '?'} min`;
}

/**
 * Format complexity score
 */
function formatComplexity(weight?: number | null): string {
  if (weight === null || weight === undefined) return 'N/A';
  return `${weight.toFixed(1)}/5.0`;
}

/**
 * Get complexity label
 */
function getComplexityLabel(weight?: number | null): string {
  if (weight === null || weight === undefined) return '';
  if (weight < 2) return 'Leggero';
  if (weight < 3) return 'Medio-Leggero';
  if (weight < 4) return 'Medio';
  if (weight < 4.5) return 'Medio-Pesante';
  return 'Pesante';
}

// ============================================================================
// Component
// ============================================================================

export function InfoGrid({
  minPlayers,
  maxPlayers,
  minPlayTimeMinutes,
  maxPlayTimeMinutes,
  averageWeight,
}: InfoGridProps) {
  return (
    <div
      className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 my-6"
      data-testid="info-grid"
    >
      {/* Player Count Card */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base font-medium">
            <Users className="h-5 w-5 text-muted-foreground" />
            Giocatori
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{formatPlayerCount(minPlayers, maxPlayers)}</div>
          <p className="text-xs text-muted-foreground mt-1">Numero di giocatori</p>
        </CardContent>
      </Card>

      {/* Play Time Card */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base font-medium">
            <Clock className="h-5 w-5 text-muted-foreground" />
            Durata
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">
            {formatPlayTime(minPlayTimeMinutes, maxPlayTimeMinutes)}
          </div>
          <p className="text-xs text-muted-foreground mt-1">Tempo di gioco</p>
        </CardContent>
      </Card>

      {/* Difficulty/Complexity Card */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base font-medium">
            <TrendingUp className="h-5 w-5 text-muted-foreground" />
            Complessità
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{formatComplexity(averageWeight)}</div>
          <p className="text-xs text-muted-foreground mt-1">
            {getComplexityLabel(averageWeight) || 'Da BoardGameGeek'}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
