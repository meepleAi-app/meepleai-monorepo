/**
 * TurnIndicatorRenderer tests (issue #1749 B19-4a).
 *
 * Covers all 7 TurnOrderType variants + null template + Rounds/TurnsPerRound +
 * phase stepper + active player + simultaneous players.
 */

import React from 'react';

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { TurnIndicatorRenderer } from '../TurnIndicatorRenderer';
import type { AiTurnTemplateSuggestion } from '@/lib/api/schemas/toolkit.schemas';

const PLAYERS = [
  { id: 'p1', name: 'Alice' },
  { id: 'p2', name: 'Bob' },
] as const;

const WINGSPAN_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'RoundRobin',
  phases: ['Round 1', 'Round 2', 'Round 3', 'Round 4'],
  rounds: 4,
  turnsPerRound: [8, 7, 6, 5],
  turnActions: ['play-bird', 'get-food', 'lay-eggs', 'draw-cards'],
  direction: 'clockwise',
};

const CODENAMES_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'Sequential',
  phases: ['Red Spymaster clue', 'Red guess', 'Blue Spymaster clue', 'Blue guess'],
};

const PALEO_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'Simultaneous',
  phases: ['Morning', 'Day', 'Night'],
};

const CAPTAIN_SONAR_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'Realtime',
  phases: ['Mission'],
};

const NONE_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'None',
  phases: [],
};

const CUSTOM_TURN: AiTurnTemplateSuggestion = {
  turnOrderType: 'Custom',
  phases: ['Setup', 'Play', 'Cleanup'],
};

describe('TurnIndicatorRenderer', () => {
  describe('empty / null template', () => {
    it('renders empty state when template is null', () => {
      render(<TurnIndicatorRenderer template={null} />);
      expect(screen.getByTestId('turn-indicator-empty')).toBeInTheDocument();
    });

    it('respects custom data-testid in empty state', () => {
      render(<TurnIndicatorRenderer template={null} data-testid="custom-turn-id" />);
      expect(screen.getByTestId('custom-turn-id')).toBeInTheDocument();
    });
  });

  describe('RoundRobin (Wingspan-like)', () => {
    it('renders round progress with TurnsPerRound', () => {
      render(
        <TurnIndicatorRenderer
          template={WINGSPAN_TURN}
          currentRound={2}
          currentTurn={3}
          activePlayer={PLAYERS[0]}
        />
      );
      const progress = screen.getByTestId('turn-round-progress');
      expect(progress).toHaveTextContent('Round 2/4');
      expect(progress).toHaveTextContent('Turn 3/7'); // round 2 has 7 turns
    });

    it('shows active player with direction', () => {
      render(<TurnIndicatorRenderer template={WINGSPAN_TURN} activePlayer={PLAYERS[1]} />);
      const badge = screen.getByTestId('turn-active-player');
      expect(badge).toHaveTextContent('Bob');
      expect(badge).toHaveTextContent('clockwise');
    });

    it('renders phase stepper with currentPhaseIndex highlight', () => {
      render(
        <TurnIndicatorRenderer
          template={WINGSPAN_TURN}
          currentPhaseIndex={1}
          activePlayer={PLAYERS[0]}
        />
      );
      const stepper = screen.getByTestId('turn-phase-stepper');
      expect(stepper).toBeInTheDocument();
      // The currently active phase has aria-current="step"
      const currentStep = stepper.querySelector('[aria-current="step"]');
      expect(currentStep).toHaveTextContent('Round 2');
    });

    it('shows waiting state when no active player', () => {
      render(<TurnIndicatorRenderer template={WINGSPAN_TURN} />);
      expect(screen.getByTestId('turn-active-player-empty')).toBeInTheDocument();
    });

    it('exposes turn order type via data attribute', () => {
      const { container } = render(
        <TurnIndicatorRenderer template={WINGSPAN_TURN} activePlayer={PLAYERS[0]} />
      );
      expect(container.querySelector('[data-turn-order-type]')).toHaveAttribute(
        'data-turn-order-type',
        'RoundRobin'
      );
    });
  });

  describe('Sequential (Codenames-like team)', () => {
    it('renders phase stepper as primary indicator', () => {
      render(<TurnIndicatorRenderer template={CODENAMES_TURN} currentPhaseIndex={0} />);
      const stepper = screen.getByTestId('turn-phase-stepper');
      expect(stepper).toBeInTheDocument();
      const currentStep = stepper.querySelector('[aria-current="step"]');
      expect(currentStep).toHaveTextContent('Red Spymaster clue');
    });

    it('omits active player badge when not provided', () => {
      render(<TurnIndicatorRenderer template={CODENAMES_TURN} />);
      expect(screen.queryByTestId('turn-active-player')).not.toBeInTheDocument();
      expect(screen.queryByTestId('turn-active-player-empty')).not.toBeInTheDocument();
    });
  });

  describe('Simultaneous (Paleo-like co-op)', () => {
    it('renders all-players badge', () => {
      render(<TurnIndicatorRenderer template={PALEO_TURN} players={PLAYERS as never} />);
      const badge = screen.getByTestId('turn-simultaneous-players');
      expect(badge).toHaveTextContent('Alice');
      expect(badge).toHaveTextContent('Bob');
      expect(badge).toHaveTextContent(/all players act simultaneously/i);
    });

    it('renders phase stepper alongside simultaneous indicator', () => {
      render(
        <TurnIndicatorRenderer
          template={PALEO_TURN}
          players={PLAYERS as never}
          currentPhaseIndex={1}
        />
      );
      expect(screen.getByTestId('turn-simultaneous-players')).toBeInTheDocument();
      const stepper = screen.getByTestId('turn-phase-stepper');
      const currentStep = stepper.querySelector('[aria-current="step"]');
      expect(currentStep).toHaveTextContent('Day');
    });

    it('handles no players gracefully', () => {
      render(<TurnIndicatorRenderer template={PALEO_TURN} />);
      expect(screen.queryByTestId('turn-simultaneous-players')).not.toBeInTheDocument();
      // But the section still renders
      expect(screen.getByTestId('turn-indicator')).toBeInTheDocument();
    });
  });

  describe('Realtime (Captain Sonar-like)', () => {
    it('renders warning banner', () => {
      render(<TurnIndicatorRenderer template={CAPTAIN_SONAR_TURN} />);
      const banner = screen.getByTestId('turn-realtime-banner');
      expect(banner).toBeInTheDocument();
      expect(banner).toHaveTextContent(/real-time play/i);
    });
  });

  describe('None (no turn structure)', () => {
    it('renders open-play banner', () => {
      render(<TurnIndicatorRenderer template={NONE_TURN} />);
      expect(screen.getByTestId('turn-none-banner')).toBeInTheDocument();
      expect(screen.getByText(/no turn order/i)).toBeInTheDocument();
    });
  });

  describe('Custom / Free', () => {
    it('renders Custom indicator', () => {
      render(<TurnIndicatorRenderer template={CUSTOM_TURN} currentPhaseIndex={1} />);
      expect(screen.getByTestId('turn-custom-indicator')).toBeInTheDocument();
      const stepper = screen.getByTestId('turn-phase-stepper');
      expect(stepper.querySelector('[aria-current="step"]')).toHaveTextContent('Play');
    });

    it('Free aliases to Custom layout', () => {
      const free: AiTurnTemplateSuggestion = { ...CUSTOM_TURN, turnOrderType: 'Free' };
      render(<TurnIndicatorRenderer template={free} />);
      expect(screen.getByTestId('turn-custom-indicator')).toBeInTheDocument();
    });
  });

  describe('unknown turnOrderType', () => {
    it('falls back to Custom layout gracefully', () => {
      const unknown: AiTurnTemplateSuggestion = {
        turnOrderType: 'TotallyInvented',
        phases: ['Phase A'],
      };
      render(<TurnIndicatorRenderer template={unknown} />);
      // Custom layout renders custom indicator
      expect(screen.getByTestId('turn-custom-indicator')).toBeInTheDocument();
    });
  });

  describe('phase stepper edge cases', () => {
    it('hides phase stepper when phases array is empty', () => {
      const noPhases: AiTurnTemplateSuggestion = {
        turnOrderType: 'RoundRobin',
        phases: [],
        rounds: 4,
        turnsPerRound: [8, 7, 6, 5],
      };
      render(<TurnIndicatorRenderer template={noPhases} activePlayer={PLAYERS[0]} />);
      expect(screen.queryByTestId('turn-phase-stepper')).not.toBeInTheDocument();
    });

    it('handles currentPhaseIndex past end gracefully (no aria-current set)', () => {
      render(<TurnIndicatorRenderer template={CUSTOM_TURN} currentPhaseIndex={99} />);
      const stepper = screen.getByTestId('turn-phase-stepper');
      // No phase is at index 99 → no aria-current
      expect(stepper.querySelector('[aria-current="step"]')).toBeNull();
    });
  });
});
