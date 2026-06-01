'use client';

import React from 'react';

import { Trophy, Hash, ListOrdered, CheckSquare, Target } from 'lucide-react';

import type {
  AiScoringTemplateSuggestion,
  AiScoringCategorySuggestion,
} from '@/lib/api/schemas/toolkit.schemas';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface ScoringPanelRendererProps {
  /** ScoringTemplate from the toolkit. Null if no scoring is configured. */
  template: AiScoringTemplateSuggestion | null;
  /** Optional per-player score values (id → score). When provided, renders read-only mini-scoreboard inline. */
  scores?: Record<string, number>;
  /** Optional player metadata for inline scoreboard. */
  players?: ReadonlyArray<{ id: string; name: string }>;
  /** Test id passthrough. */
  'data-testid'?: string;
}

// ── Empty state ───────────────────────────────────────────────────────────────

function ScoringEmpty({ testId }: { testId?: string }) {
  return (
    <div
      className="flex min-h-32 flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-muted/30 p-4 text-muted-foreground"
      data-testid={testId ?? 'scoring-panel-empty'}
    >
      <Trophy className="h-8 w-8 opacity-30" aria-hidden="true" />
      <p className="text-sm">No scoring template configured for this game.</p>
    </div>
  );
}

// ── Per-category computation icon ─────────────────────────────────────────────

function CategoryIcon({
  computation,
}: {
  computation: AiScoringCategorySuggestion['computation'];
}) {
  const className = 'h-4 w-4';
  switch (computation) {
    case 'Count':
      return <Hash className={className} aria-hidden="true" />;
    case 'Sum':
      return <ListOrdered className={className} aria-hidden="true" />;
    case 'RankBased':
      return <Trophy className={className} aria-hidden="true" />;
    case 'Custom':
      return <CheckSquare className={className} aria-hidden="true" />;
    default:
      return <Target className={className} aria-hidden="true" />;
  }
}

// ── ScoreType-specific layouts ────────────────────────────────────────────────

function CategoryRow({ category }: { category: AiScoringCategorySuggestion }) {
  return (
    <div
      className="flex items-start gap-3 rounded-md border border-border bg-card p-3"
      data-testid={`scoring-category-${category.id}`}
      data-computation={category.computation}
    >
      <div className="mt-0.5 shrink-0 text-muted-foreground">
        <CategoryIcon computation={category.computation} />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <p className="truncate font-medium text-foreground">{category.label}</p>
          {category.weight !== 1 && (
            <span className="shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
              ×{category.weight}
            </span>
          )}
        </div>
        {category.description && (
          <p className="mt-1 text-xs text-muted-foreground">{category.description}</p>
        )}
      </div>
    </div>
  );
}

function PointsLayout({
  template,
  scores,
  players,
}: {
  template: AiScoringTemplateSuggestion;
  scores?: Record<string, number>;
  players?: ReadonlyArray<{ id: string; name: string }>;
}) {
  const categories = template.categories ?? null;
  return (
    <div className="space-y-3">
      {categories && categories.length > 0 ? (
        <div className="space-y-2" data-testid="scoring-categories">
          {categories.map(c => (
            <CategoryRow key={c.id} category={c} />
          ))}
        </div>
      ) : (
        <ul
          className="flex flex-wrap gap-2"
          data-testid="scoring-dimensions-legacy"
          aria-label="Scoring dimensions"
        >
          {template.dimensions.map(dim => (
            <li key={dim} className="rounded-full bg-muted px-3 py-1 text-sm text-foreground">
              {dim}
            </li>
          ))}
        </ul>
      )}
      {scores && players && players.length > 0 && (
        <InlineScoreboard scores={scores} players={players} unit={template.defaultUnit} />
      )}
    </div>
  );
}

function RankingLayout({
  template,
  scores,
  players,
}: {
  template: AiScoringTemplateSuggestion;
  scores?: Record<string, number>;
  players?: ReadonlyArray<{ id: string; name: string }>;
}) {
  // Ranking: prefer scores when provided, sort descending
  if (scores && players && players.length > 0) {
    const ranked = players
      .map(p => ({ ...p, score: scores[p.id] ?? 0 }))
      .sort((a, b) => b.score - a.score);
    return (
      <ol className="space-y-1" data-testid="scoring-ranking">
        {ranked.map((p, i) => (
          <li
            key={p.id}
            className="flex items-center justify-between gap-2 rounded-md border border-border bg-card px-3 py-2"
          >
            <span className="flex items-center gap-2">
              <span
                className={`inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-semibold ${
                  i === 0 ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'
                }`}
              >
                {i + 1}
              </span>
              <span className="text-foreground">{p.name}</span>
            </span>
            <span className="font-mono text-sm text-muted-foreground">
              {p.score} {template.defaultUnit}
            </span>
          </li>
        ))}
      </ol>
    );
  }
  return (
    <p className="text-sm text-muted-foreground" data-testid="scoring-ranking-empty">
      Ranking will appear once scores are recorded.
    </p>
  );
}

function BinaryWinLayout({ scores }: { scores?: Record<string, number> }) {
  // BinaryWin: no points, just win/lose collective. Show generic indicator.
  const hasScore = scores && Object.keys(scores).length > 0;
  return (
    <div
      className="flex flex-col items-center gap-2 rounded-md border border-border bg-card p-4 text-center"
      data-testid="scoring-binary-win"
    >
      <Target className="h-8 w-8 text-primary" aria-hidden="true" />
      <p className="text-sm font-medium text-foreground">
        {hasScore ? 'Outcome recorded — see session summary.' : 'Win/lose tracked at game end.'}
      </p>
      <p className="text-xs text-muted-foreground">
        This game has no continuous scoring; the result is collective.
      </p>
    </div>
  );
}

function ObjectivesLayout({
  template,
  scores,
}: {
  template: AiScoringTemplateSuggestion;
  scores?: Record<string, number>;
}) {
  // Objectives: list of dimensions as checkboxes (read-only, requires controlled state for interaction)
  return (
    <div className="space-y-2" data-testid="scoring-objectives">
      {template.dimensions.map(dim => (
        <div
          key={dim}
          className="flex items-center gap-3 rounded-md border border-border bg-card p-3"
        >
          <CheckSquare className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
          <span className="flex-1 text-sm text-foreground">{dim}</span>
          {scores?.[dim] !== undefined && (
            <span className="font-mono text-xs text-muted-foreground">{scores[dim]}</span>
          )}
        </div>
      ))}
    </div>
  );
}

function InlineScoreboard({
  scores,
  players,
  unit,
}: {
  scores: Record<string, number>;
  players: ReadonlyArray<{ id: string; name: string }>;
  unit: string;
}) {
  return (
    <div
      className="rounded-md border border-border bg-muted/30 p-3"
      data-testid="scoring-inline-scoreboard"
    >
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Live scoreboard
      </p>
      <ul className="space-y-1">
        {players.map(p => (
          <li key={p.id} className="flex items-center justify-between text-sm">
            <span className="text-foreground">{p.name}</span>
            <span className="font-mono text-muted-foreground">
              {scores[p.id] ?? 0} {unit}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ── Main dispatcher ───────────────────────────────────────────────────────────

/**
 * ScoringPanelRenderer — polymorphic UI for the ScoringTemplate of a toolkit.
 *
 * Issue #1749 (B19-4a). Switches on ScoreType to render the most appropriate
 * UI for each scoring model:
 *
 * - **Points**: list of scoring categories (when v3 Categories[] provided)
 *   or flat list of dimensions (legacy); optional inline scoreboard.
 * - **Ranking**: live ordered list with rank pills; falls back to placeholder
 *   when no scores recorded yet.
 * - **BinaryWin**: collective outcome indicator (co-op games like Paleo).
 * - **Objectives**: checklist of objectives (goal-oriented games like Codenames).
 *
 * Unknown ScoreType falls back to Points layout for graceful degradation.
 */
export function ScoringPanelRenderer({
  template,
  scores,
  players,
  'data-testid': testId,
}: ScoringPanelRendererProps) {
  if (!template) {
    return <ScoringEmpty testId={testId} />;
  }

  const scoreType = template.scoreType;
  let body: React.ReactNode;
  switch (scoreType) {
    case 'Points':
      body = <PointsLayout template={template} scores={scores} players={players} />;
      break;
    case 'Ranking':
      body = <RankingLayout template={template} scores={scores} players={players} />;
      break;
    case 'BinaryWin':
      body = <BinaryWinLayout scores={scores} />;
      break;
    case 'Objectives':
      body = <ObjectivesLayout template={template} scores={scores} />;
      break;
    default:
      // Graceful degradation: unknown ScoreType → render as Points
      body = <PointsLayout template={template} scores={scores} players={players} />;
      break;
  }

  return (
    <section
      className="flex flex-col gap-3"
      data-testid={testId ?? 'scoring-panel'}
      data-score-type={scoreType}
      aria-label="Scoring panel"
    >
      {body}
    </section>
  );
}
