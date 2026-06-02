/**
 * ScoringPanelRenderer tests (issue #1749 B19-4a).
 *
 * Covers all 4 ScoreType variants + null template + Categories[] vs legacy
 * Dimensions[] back-compat + inline scoreboard rendering.
 */

import React from 'react';

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ScoringPanelRenderer } from '../ScoringPanelRenderer';
import type {
  AiScoringTemplateSuggestion,
  AiScoringCategorySuggestion,
} from '@/lib/api/schemas/toolkit.schemas';

const PLAYERS = [
  { id: 'p1', name: 'Alice' },
  { id: 'p2', name: 'Bob' },
  { id: 'p3', name: 'Carol' },
] as const;

const WINGSPAN_CATEGORIES: AiScoringCategorySuggestion[] = [
  { id: 'birds', label: 'Birds played', computation: 'Sum', weight: 1 },
  { id: 'bonus', label: 'Bonus cards', computation: 'Sum', weight: 1 },
  { id: 'eggs', label: 'Eggs', computation: 'Count', weight: 1 },
  {
    id: 'round-goals',
    label: 'End-of-round goals',
    computation: 'RankBased',
    weight: 1,
    description: '5/2/1',
  },
];

const WINGSPAN_TEMPLATE: AiScoringTemplateSuggestion = {
  dimensions: ['Birds', 'Bonus cards', 'Eggs', 'End-of-round goals'],
  defaultUnit: 'points',
  scoreType: 'Points',
  categories: WINGSPAN_CATEGORIES,
};

const LEGACY_TEMPLATE: AiScoringTemplateSuggestion = {
  dimensions: ['Victory Points', 'Settlements'],
  defaultUnit: 'points',
  scoreType: 'Points',
  // No categories — legacy shape
};

const CATAN_RANKING: AiScoringTemplateSuggestion = {
  dimensions: ['Total VP'],
  defaultUnit: 'points',
  scoreType: 'Ranking',
};

const PALEO_BINARY: AiScoringTemplateSuggestion = {
  dimensions: ['Win condition'],
  defaultUnit: 'result',
  scoreType: 'BinaryWin',
};

const CODENAMES_OBJECTIVES: AiScoringTemplateSuggestion = {
  dimensions: ['Red agents revealed', 'Blue agents revealed', 'Assassin avoided'],
  defaultUnit: 'tiles',
  scoreType: 'Objectives',
};

describe('ScoringPanelRenderer', () => {
  describe('empty / null template', () => {
    it('renders empty state when template is null', () => {
      render(<ScoringPanelRenderer template={null} />);
      expect(screen.getByTestId('scoring-panel-empty')).toBeInTheDocument();
      expect(screen.getByText(/no scoring template/i)).toBeInTheDocument();
    });

    it('respects custom data-testid in empty state', () => {
      render(<ScoringPanelRenderer template={null} data-testid="custom-id" />);
      expect(screen.getByTestId('custom-id')).toBeInTheDocument();
    });
  });

  describe('Points layout (Wingspan v3 categories)', () => {
    it('renders all 4 categories with their computation icons', () => {
      render(<ScoringPanelRenderer template={WINGSPAN_TEMPLATE} />);
      expect(screen.getByTestId('scoring-categories')).toBeInTheDocument();
      WINGSPAN_CATEGORIES.forEach(c => {
        expect(screen.getByTestId(`scoring-category-${c.id}`)).toBeInTheDocument();
        expect(screen.getByText(c.label)).toBeInTheDocument();
      });
    });

    it('exposes computation type via data attribute for testability', () => {
      render(<ScoringPanelRenderer template={WINGSPAN_TEMPLATE} />);
      expect(screen.getByTestId('scoring-category-birds')).toHaveAttribute(
        'data-computation',
        'Sum'
      );
      expect(screen.getByTestId('scoring-category-eggs')).toHaveAttribute(
        'data-computation',
        'Count'
      );
      expect(screen.getByTestId('scoring-category-round-goals')).toHaveAttribute(
        'data-computation',
        'RankBased'
      );
    });

    it('shows description when category has one', () => {
      render(<ScoringPanelRenderer template={WINGSPAN_TEMPLATE} />);
      expect(screen.getByText('5/2/1')).toBeInTheDocument();
    });

    it('shows weight badge only when weight !== 1', () => {
      const customWeight: AiScoringTemplateSuggestion = {
        ...WINGSPAN_TEMPLATE,
        categories: [
          { id: 'doubled', label: 'Doubled cat', computation: 'Sum', weight: 2 },
          { id: 'normal', label: 'Normal cat', computation: 'Sum', weight: 1 },
        ],
      };
      render(<ScoringPanelRenderer template={customWeight} />);
      expect(screen.getByText('×2')).toBeInTheDocument();
      expect(screen.queryByText('×1')).not.toBeInTheDocument();
    });
  });

  describe('Points layout (legacy dimensions only)', () => {
    it('falls back to legacy dimensions chip list when no categories', () => {
      render(<ScoringPanelRenderer template={LEGACY_TEMPLATE} />);
      expect(screen.getByTestId('scoring-dimensions-legacy')).toBeInTheDocument();
      expect(screen.queryByTestId('scoring-categories')).not.toBeInTheDocument();
      LEGACY_TEMPLATE.dimensions.forEach(d => {
        expect(screen.getByText(d)).toBeInTheDocument();
      });
    });
  });

  describe('Ranking layout', () => {
    it('renders ranked list with rank pills when scores provided', () => {
      const scores = { p1: 12, p2: 8, p3: 15 };
      render(
        <ScoringPanelRenderer template={CATAN_RANKING} scores={scores} players={PLAYERS as never} />
      );
      const ranking = screen.getByTestId('scoring-ranking');
      expect(ranking).toBeInTheDocument();
      const items = ranking.querySelectorAll('li');
      expect(items).toHaveLength(3);
      // Carol (15) > Alice (12) > Bob (8)
      expect(items[0]).toHaveTextContent('Carol');
      expect(items[1]).toHaveTextContent('Alice');
      expect(items[2]).toHaveTextContent('Bob');
    });

    it('shows placeholder when no scores recorded yet', () => {
      render(<ScoringPanelRenderer template={CATAN_RANKING} players={PLAYERS as never} />);
      expect(screen.getByTestId('scoring-ranking-empty')).toBeInTheDocument();
    });

    it('handles missing player scores as 0', () => {
      const scores = { p1: 10 };
      render(
        <ScoringPanelRenderer template={CATAN_RANKING} scores={scores} players={PLAYERS as never} />
      );
      const ranking = screen.getByTestId('scoring-ranking');
      // p2 and p3 missing → both 0, p1 wins
      expect(ranking.querySelectorAll('li')[0]).toHaveTextContent('Alice');
    });
  });

  describe('BinaryWin layout', () => {
    it('renders collective outcome indicator', () => {
      render(<ScoringPanelRenderer template={PALEO_BINARY} />);
      expect(screen.getByTestId('scoring-binary-win')).toBeInTheDocument();
      expect(screen.getByText(/win\/lose tracked at game end/i)).toBeInTheDocument();
    });

    it('changes message when scores present', () => {
      render(<ScoringPanelRenderer template={PALEO_BINARY} scores={{ result: 1 }} />);
      expect(screen.getByText(/outcome recorded/i)).toBeInTheDocument();
    });
  });

  describe('Objectives layout', () => {
    it('renders each dimension as a checkable row', () => {
      render(<ScoringPanelRenderer template={CODENAMES_OBJECTIVES} />);
      expect(screen.getByTestId('scoring-objectives')).toBeInTheDocument();
      CODENAMES_OBJECTIVES.dimensions.forEach(d => {
        expect(screen.getByText(d)).toBeInTheDocument();
      });
    });
  });

  describe('inline scoreboard (Points mode)', () => {
    it('renders inline scoreboard when scores+players provided', () => {
      const scores = { p1: 25, p2: 18, p3: 22 };
      render(
        <ScoringPanelRenderer
          template={WINGSPAN_TEMPLATE}
          scores={scores}
          players={PLAYERS as never}
        />
      );
      const board = screen.getByTestId('scoring-inline-scoreboard');
      expect(board).toBeInTheDocument();
      // Verify it renders 3 players with their scores
      expect(board).toHaveTextContent('Alice');
      expect(board).toHaveTextContent('25');
      expect(board).toHaveTextContent('Bob');
      expect(board).toHaveTextContent('18');
    });

    it('skips inline scoreboard when only players provided without scores', () => {
      render(<ScoringPanelRenderer template={WINGSPAN_TEMPLATE} players={PLAYERS as never} />);
      expect(screen.queryByTestId('scoring-inline-scoreboard')).not.toBeInTheDocument();
    });
  });

  describe('unknown scoreType', () => {
    it('falls back to Points layout for unknown scoreType', () => {
      const unknown: AiScoringTemplateSuggestion = {
        ...LEGACY_TEMPLATE,
        scoreType: 'TotallyMadeUp',
      };
      const { container } = render(<ScoringPanelRenderer template={unknown} />);
      // Should not crash, should render legacy dimensions
      expect(screen.getByTestId('scoring-dimensions-legacy')).toBeInTheDocument();
      // Section data attribute reflects the (unknown) score type for debugging
      const section = container.querySelector('[data-score-type]');
      expect(section).toHaveAttribute('data-score-type', 'TotallyMadeUp');
    });
  });
});
