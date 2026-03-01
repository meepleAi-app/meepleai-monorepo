/**
 * resolveSessionTools unit tests (Issue #4976).
 */

import { describe, it, expect } from 'vitest';

import type { GameToolkitDto } from '../../types/gameToolkit';
import { resolveSessionTools } from '../resolveSessionTools';

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeToolkit(overrides: Partial<GameToolkitDto> = {}): GameToolkitDto {
  return {
    id: 'toolkit-1',
    gameId: 'game-1',
    privateGameId: null,
    name: 'Test Toolkit',
    version: 1,
    isPublished: true,
    overridesTurnOrder: false,
    overridesScoreboard: false,
    overridesDiceSet: false,
    diceTools: [],
    cardTools: [],
    timerTools: [],
    counterTools: [],
    ...overrides,
  };
}

// ── No toolkit ────────────────────────────────────────────────────────────────

describe('resolveSessionTools — no toolkit', () => {
  it('returns all 4 base tools when toolkit is null', () => {
    const result = resolveSessionTools(null);
    expect(result.visibleBaseToolIds).toContain('scoreboard');
    expect(result.visibleBaseToolIds).toContain('turn-order');
    expect(result.visibleBaseToolIds).toContain('dice');
    expect(result.visibleBaseToolIds).toContain('whiteboard');
    expect(result.customTools).toHaveLength(0);
  });

  it('returns all 4 base tools when toolkit is undefined', () => {
    const result = resolveSessionTools(undefined);
    expect(result.visibleBaseToolIds.size).toBe(4);
    expect(result.customTools).toHaveLength(0);
  });

  it('returns all 4 base tools when toolkit is unpublished', () => {
    const result = resolveSessionTools(makeToolkit({ isPublished: false }));
    expect(result.visibleBaseToolIds.size).toBe(4);
    expect(result.customTools).toHaveLength(0);
  });
});

// ── Override flags ────────────────────────────────────────────────────────────

describe('resolveSessionTools — override flags', () => {
  it('hides turn-order when overridesTurnOrder is true', () => {
    const result = resolveSessionTools(makeToolkit({ overridesTurnOrder: true }));
    expect(result.visibleBaseToolIds).not.toContain('turn-order');
    expect(result.visibleBaseToolIds).toContain('scoreboard');
    expect(result.visibleBaseToolIds).toContain('dice');
    expect(result.visibleBaseToolIds).toContain('whiteboard');
  });

  it('hides scoreboard when overridesScoreboard is true', () => {
    const result = resolveSessionTools(makeToolkit({ overridesScoreboard: true }));
    expect(result.visibleBaseToolIds).not.toContain('scoreboard');
    expect(result.visibleBaseToolIds).toContain('turn-order');
    expect(result.visibleBaseToolIds).toContain('dice');
    expect(result.visibleBaseToolIds).toContain('whiteboard');
  });

  it('hides dice when overridesDiceSet is true', () => {
    const result = resolveSessionTools(makeToolkit({ overridesDiceSet: true }));
    expect(result.visibleBaseToolIds).not.toContain('dice');
    expect(result.visibleBaseToolIds).toContain('scoreboard');
    expect(result.visibleBaseToolIds).toContain('turn-order');
    expect(result.visibleBaseToolIds).toContain('whiteboard');
  });

  it('whiteboard is never hidden regardless of override flags', () => {
    const result = resolveSessionTools(
      makeToolkit({
        overridesTurnOrder: true,
        overridesScoreboard: true,
        overridesDiceSet: true,
      }),
    );
    expect(result.visibleBaseToolIds).toContain('whiteboard');
    expect(result.visibleBaseToolIds.size).toBe(1); // only whiteboard remains
  });

  it('can hide all three overridable base tools simultaneously', () => {
    const result = resolveSessionTools(
      makeToolkit({
        overridesTurnOrder: true,
        overridesScoreboard: true,
        overridesDiceSet: true,
      }),
    );
    expect(result.visibleBaseToolIds).not.toContain('turn-order');
    expect(result.visibleBaseToolIds).not.toContain('scoreboard');
    expect(result.visibleBaseToolIds).not.toContain('dice');
  });
});

// ── Extra tools ───────────────────────────────────────────────────────────────

describe('resolveSessionTools — extra tools', () => {
  it('maps counter tools to custom ToolItems', () => {
    const result = resolveSessionTools(
      makeToolkit({
        counterTools: [
          { name: 'Gold', minValue: 0, maxValue: 100, defaultValue: 0, isPerPlayer: true },
          { name: 'VP', minValue: 0, maxValue: 50, defaultValue: 0, isPerPlayer: false },
        ],
      }),
    );
    expect(result.customTools).toHaveLength(2);
    expect(result.customTools[0].id).toBe('custom-counter-0');
    expect(result.customTools[0].label).toBe('Gold');
    expect(result.customTools[0].type).toBe('custom');
    expect(result.customTools[1].id).toBe('custom-counter-1');
    expect(result.customTools[1].label).toBe('VP');
  });

  it('maps dice tools to custom ToolItems', () => {
    const result = resolveSessionTools(
      makeToolkit({
        diceTools: [
          { name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true },
        ],
      }),
    );
    expect(result.customTools).toHaveLength(1);
    expect(result.customTools[0].id).toBe('custom-dice-0');
    expect(result.customTools[0].label).toBe('2×d6');
    expect(result.customTools[0].type).toBe('custom');
  });

  it('maps card tools to custom ToolItems', () => {
    const result = resolveSessionTools(
      makeToolkit({
        cardTools: [
          {
            name: 'Deck sviluppo',
            deckType: 'Custom',
            cardCount: 54,
            shuffleable: true,
            allowDraw: true,
            allowDiscard: true,
            allowPeek: false,
            allowReturnToDeck: false,
          },
        ],
      }),
    );
    expect(result.customTools).toHaveLength(1);
    expect(result.customTools[0].id).toBe('custom-card-0');
    expect(result.customTools[0].label).toBe('Deck sviluppo');
  });

  it('maps timer tools to custom ToolItems', () => {
    const result = resolveSessionTools(
      makeToolkit({
        timerTools: [
          {
            name: 'Countdown',
            durationSeconds: 60,
            timerType: 'countdown',
            autoStart: false,
            isPerPlayer: false,
          },
        ],
      }),
    );
    expect(result.customTools).toHaveLength(1);
    expect(result.customTools[0].id).toBe('custom-timer-0');
    expect(result.customTools[0].label).toBe('Countdown');
  });

  it('orders custom tools: dice → card → timer → counter', () => {
    const result = resolveSessionTools(
      makeToolkit({
        diceTools: [{ name: 'Dice', diceType: 'd6', quantity: 1, isInteractive: true }],
        cardTools: [
          {
            name: 'Cards',
            deckType: 'Standard52',
            cardCount: 52,
            shuffleable: true,
            allowDraw: true,
            allowDiscard: true,
            allowPeek: false,
            allowReturnToDeck: false,
          },
        ],
        timerTools: [
          {
            name: 'Timer',
            durationSeconds: 30,
            timerType: 'countdown',
            autoStart: false,
            isPerPlayer: false,
          },
        ],
        counterTools: [
          { name: 'Counter', minValue: 0, maxValue: 10, defaultValue: 0, isPerPlayer: false },
        ],
      }),
    );
    expect(result.customTools).toHaveLength(4);
    expect(result.customTools[0].id).toBe('custom-dice-0');
    expect(result.customTools[1].id).toBe('custom-card-0');
    expect(result.customTools[2].id).toBe('custom-timer-0');
    expect(result.customTools[3].id).toBe('custom-counter-0');
  });

  it('truncates long labels for shortLabel', () => {
    const result = resolveSessionTools(
      makeToolkit({
        counterTools: [
          {
            name: 'Very Long Counter Name',
            minValue: 0,
            maxValue: 100,
            defaultValue: 0,
            isPerPlayer: false,
          },
        ],
      }),
    );
    expect(result.customTools[0].shortLabel.length).toBeLessThanOrEqual(6);
  });
});

// ── Combined ─────────────────────────────────────────────────────────────────

describe('resolveSessionTools — combined override + extra tools', () => {
  it('hides base tool and adds custom tools simultaneously', () => {
    const result = resolveSessionTools(
      makeToolkit({
        overridesDiceSet: true,
        diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
      }),
    );
    expect(result.visibleBaseToolIds).not.toContain('dice');
    expect(result.customTools).toHaveLength(1);
    expect(result.customTools[0].id).toBe('custom-dice-0');
  });
});
