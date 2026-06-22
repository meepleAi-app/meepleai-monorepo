/**
 * PlayRecordHeroPodium — Task 0.6 (Issue #1488).
 *
 * Variant-driven hero used at the top of `/play-records/[id]` detail view.
 *
 * 5 variants matching the plan AC-2.2 matrix:
 *   1. won         — confetti + 🏆 + ranked podium (top-3)
 *   2. tied        — 🤝 banner + 2+ crowns on shared 1st place
 *   3. cooperative — no crown, neutral classifica banner
 *   4. inprogress  — pulsing "In corso · turno N" badge (no confetti)
 *   5. planned     — calendar badge + "Avvia partita" CTA
 *
 * Distinct from `features/session-summary/SessionSummaryHero`:
 *   - session-summary expects RankedParticipant (post-game)
 *   - play-records accepts raw rankedScores + variant + game cover/emoji
 *   - play-records supports `inprogress` and `planned` (no equivalent in
 *     session-summary, which only renders completed games)
 *
 * Pure component — all i18n strings injected via `labels` prop.
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PlayRecordHeroPodium, type PlayRecordHeroPodiumProps } from '../PlayRecordHeroPodium';

const LABELS: PlayRecordHeroPodiumProps['labels'] = {
  variantWon: '🏆 Vittoria',
  variantTied: '🤝 Pareggio',
  variantCooperative: '🤝 Cooperativa',
  variantInProgress: 'In corso',
  variantPlanned: '📅 Pianificata',
  bannerWon: (name: string, game: string) => `${name} vince ${game}`,
  bannerTied: (score: number) => `Pareggio a ${score} punti`,
  bannerCooperative: (game: string) => `Partita cooperativa di ${game}`,
  bannerInProgress: (game: string, turn?: number) =>
    turn !== undefined ? `${game} in corso · turno ${turn}` : `${game} in corso`,
  bannerPlanned: (game: string) => `${game} pianificata`,
  metaPlayers: (n: number) => `${n} giocatori`,
  ctaStart: 'Avvia partita',
};

const MOCK_GAME: PlayRecordHeroPodiumProps['game'] = {
  id: 'g-1',
  name: 'Wingspan',
  coverEmoji: '🦅',
};

const MOCK_GAME_FREEFORM: PlayRecordHeroPodiumProps['game'] = {
  id: null,
  name: 'Una partita strana',
  coverEmoji: '🎲',
};

const MOCK_SCORES: PlayRecordHeroPodiumProps['rankedScores'] = [
  { playerId: 'p-1', name: 'Marco', score: 85, isWinner: true },
  { playerId: 'p-2', name: 'Anna', score: 70, isWinner: false },
  { playerId: 'p-3', name: 'Luca', score: 60, isWinner: false },
];

const MOCK_SCORES_TIE: PlayRecordHeroPodiumProps['rankedScores'] = [
  { playerId: 'p-1', name: 'Marco', score: 85, isWinner: true },
  { playerId: 'p-2', name: 'Anna', score: 85, isWinner: true },
  { playerId: 'p-3', name: 'Luca', score: 60, isWinner: false },
];

const MOCK_METADATA: PlayRecordHeroPodiumProps['metadata'] = {
  date: '5 mag',
  duration: '2h 15min',
  playerCount: 3,
};

function podium(extra: Partial<PlayRecordHeroPodiumProps>) {
  const defaults: PlayRecordHeroPodiumProps = {
    variant: 'won',
    game: MOCK_GAME,
    rankedScores: MOCK_SCORES,
    metadata: MOCK_METADATA,
    perspective: { kind: 'won', currentUserPlayerId: 'p-1' },
    labels: LABELS,
  };
  return render(<PlayRecordHeroPodium {...defaults} {...extra} />);
}

describe('PlayRecordHeroPodium', () => {
  describe('variant matrix', () => {
    it('won variant renders data-variant + winner banner', () => {
      podium({ variant: 'won' });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root).not.toBeNull();
      expect(root!.getAttribute('data-variant')).toBe('won');
      expect(screen.getByText('Marco vince Wingspan')).toBeTruthy();
    });

    it('tied variant renders tied banner with top score', () => {
      podium({
        variant: 'tied',
        rankedScores: MOCK_SCORES_TIE,
        perspective: { kind: 'tie', currentUserPlayerId: 'p-1' },
      });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root!.getAttribute('data-variant')).toBe('tied');
      expect(screen.getByText('Pareggio a 85 punti')).toBeTruthy();
    });

    it('cooperative variant renders cooperative banner, no winner crown', () => {
      podium({
        variant: 'cooperative',
        rankedScores: MOCK_SCORES,
        perspective: { kind: 'cooperative', currentUserPlayerId: 'p-1' },
      });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root!.getAttribute('data-variant')).toBe('cooperative');
      expect(screen.getByText('Partita cooperativa di Wingspan')).toBeTruthy();
      // No crown emoji in cooperative
      expect(document.querySelector('[data-slot="podium-winner-crown"]')).toBeNull();
    });

    it('inprogress variant renders pulsing badge with turn', () => {
      podium({
        variant: 'inprogress',
        metadata: { ...MOCK_METADATA, duration: null, turn: 5 },
        perspective: { kind: 'pending', currentUserPlayerId: 'p-1' },
      });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root!.getAttribute('data-variant')).toBe('inprogress');
      const badge = root!.querySelector('[data-slot="hero-badge"]');
      expect(badge).not.toBeNull();
      expect(screen.getByText('Wingspan in corso · turno 5')).toBeTruthy();
    });

    it('planned variant renders calendar badge + start CTA', () => {
      podium({
        variant: 'planned',
        metadata: { ...MOCK_METADATA, duration: null },
        perspective: { kind: 'pending', currentUserPlayerId: 'p-1' },
      });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root!.getAttribute('data-variant')).toBe('planned');
      expect(screen.getByText('Avvia partita')).toBeTruthy();
    });
  });

  describe('a11y', () => {
    it('uses section + aria-labelledby on the heading', () => {
      const { container } = podium({ variant: 'won' });
      const section = container.querySelector('section[data-slot="play-record-hero-podium"]');
      expect(section).not.toBeNull();
      const labelledBy = section!.getAttribute('aria-labelledby');
      expect(labelledBy).not.toBeNull();
      const heading = document.getElementById(labelledBy!);
      expect(heading).not.toBeNull();
    });

    it('inprogress variant carries role="status" on its badge', () => {
      podium({ variant: 'inprogress', metadata: { ...MOCK_METADATA, turn: 1 } });
      const badge = document.querySelector('[data-slot="hero-badge"]');
      expect(badge!.getAttribute('role')).toBe('status');
    });
  });

  describe('edge cases', () => {
    it('EC-2 freeform game (gameId=null) renders coverEmoji fallback', () => {
      podium({ variant: 'planned', game: MOCK_GAME_FREEFORM });
      // The cover element exists (presentational); the test verifies it's rendered
      const cover = document.querySelector('[data-slot="hero-cover"]');
      expect(cover).not.toBeNull();
      expect(cover!.textContent).toContain('🎲');
    });

    it('won variant renders crown only on rank-1 podium spot', () => {
      podium({ variant: 'won' });
      const crowns = document.querySelectorAll('[data-slot="podium-winner-crown"]');
      expect(crowns.length).toBe(1);
    });

    it('tied variant renders crown on each tied 1st-place spot', () => {
      podium({
        variant: 'tied',
        rankedScores: MOCK_SCORES_TIE,
        perspective: { kind: 'tie', currentUserPlayerId: 'p-1' },
      });
      const crowns = document.querySelectorAll('[data-slot="podium-winner-crown"]');
      expect(crowns.length).toBe(2);
    });

    it('renders metadata date + player count', () => {
      podium({ variant: 'won' });
      expect(screen.getByText(/5 mag/)).toBeTruthy();
      expect(screen.getByText(/3 giocatori/)).toBeTruthy();
    });

    it('inprogress variant omits duration from metadata (EC-6)', () => {
      podium({
        variant: 'inprogress',
        metadata: { ...MOCK_METADATA, duration: null, turn: 1 },
      });
      // Duration should NOT appear when null
      expect(screen.queryByText(/2h 15min/)).toBeNull();
    });

    it('accepts custom className', () => {
      podium({ variant: 'won', className: 'hero-test-class' });
      const root = document.querySelector('[data-slot="play-record-hero-podium"]');
      expect(root!.classList.contains('hero-test-class')).toBe(true);
    });

    it('renders podium for 1 player (no flanks, just center)', () => {
      podium({
        variant: 'won',
        rankedScores: [{ playerId: 'p-1', name: 'Marco', score: 85, isWinner: true }],
        metadata: { ...MOCK_METADATA, playerCount: 1 },
      });
      const places = document.querySelectorAll('[data-slot="podium-place"]');
      expect(places.length).toBe(1);
    });

    it('renders podium for 2 players (center + one flank)', () => {
      podium({
        variant: 'won',
        rankedScores: MOCK_SCORES.slice(0, 2),
        metadata: { ...MOCK_METADATA, playerCount: 2 },
      });
      const places = document.querySelectorAll('[data-slot="podium-place"]');
      expect(places.length).toBe(2);
    });

    it('renders podium for 4+ players (top-3 only)', () => {
      const fourScores: PlayRecordHeroPodiumProps['rankedScores'] = [
        ...MOCK_SCORES,
        { playerId: 'p-4', name: 'Sara', score: 50, isWinner: false },
      ];
      podium({
        variant: 'won',
        rankedScores: fourScores,
        metadata: { ...MOCK_METADATA, playerCount: 4 },
      });
      const places = document.querySelectorAll('[data-slot="podium-place"]');
      expect(places.length).toBe(3);
    });

    it('highlights currentUser place when present in podium', () => {
      podium({ variant: 'won', perspective: { kind: 'won', currentUserPlayerId: 'p-1' } });
      const meSpot = document.querySelector('[data-slot="podium-place"][data-current-user="true"]');
      expect(meSpot).not.toBeNull();
    });

    it('does NOT highlight any spot when currentUser is spectator', () => {
      podium({ variant: 'won', perspective: { kind: 'spectator', currentUserPlayerId: null } });
      const meSpot = document.querySelector('[data-slot="podium-place"][data-current-user="true"]');
      expect(meSpot).toBeNull();
    });

    it('handles null score gracefully (renders em dash placeholder)', () => {
      podium({
        variant: 'inprogress',
        metadata: { ...MOCK_METADATA, duration: null, turn: 2 },
        rankedScores: [
          { playerId: 'p-1', name: 'Marco', score: null, isWinner: false },
          { playerId: 'p-2', name: 'Anna', score: null, isWinner: false },
        ],
      });
      // No assertion error; just ensure render doesn't crash and em-dash placeholder appears
      const dashes = screen.getAllByText('—');
      expect(dashes.length).toBeGreaterThanOrEqual(1);
    });
  });
});
