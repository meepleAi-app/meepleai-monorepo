'use client';

/**
 * useDiceRoller — Custom hook for cryptographically random dice rolling.
 *
 * Parses formulas like "2d6+1d8+3" and rolls each die using
 * crypto.getRandomValues() for fair randomness.
 */

import { useCallback, useRef, useState } from 'react';

import type { DiceResult } from '../types';

interface ParsedDie {
  count: number;
  sides: number;
}

interface ParsedFormula {
  dice: ParsedDie[];
  modifier: number;
}

/**
 * Parses a dice formula string into structured data.
 *
 * Supported formats:
 *  - "2d6"         → 2 six-sided dice, modifier 0
 *  - "2d6+1d8+3"  → 2d6 + 1d8, modifier +3
 *  - "1d20-2"     → 1d20, modifier -2
 */
export function parseDiceFormula(formula: string): ParsedFormula {
  const cleaned = formula.replace(/\s/g, '').toLowerCase();
  const dice: ParsedDie[] = [];
  let modifier = 0;

  // Split on + or - while keeping the sign
  const tokens = cleaned.split(/(?=[+-])/);

  for (const token of tokens) {
    const diceMatch = token.match(/^([+-]?)(\d*)d(\d+)$/);
    if (diceMatch) {
      const sign = diceMatch[1] === '-' ? -1 : 1;
      const count = sign * (diceMatch[2] ? parseInt(diceMatch[2], 10) : 1);
      const sides = parseInt(diceMatch[3], 10);
      if (sides > 0) {
        dice.push({ count, sides });
      }
    } else {
      // Plain number modifier
      const num = parseInt(token, 10);
      if (!isNaN(num)) {
        modifier += num;
      }
    }
  }

  return { dice, modifier };
}

/** Roll a single die with sides using crypto.getRandomValues(). */
function rollSingleDie(sides: number): number {
  const array = new Uint32Array(1);
  crypto.getRandomValues(array);
  return (array[0] % sides) + 1;
}

/** Execute a full dice roll from a formula string. */
export function rollDiceFromFormula(formula: string): DiceResult {
  const { dice, modifier } = parseDiceFormula(formula);
  const rolls: number[] = [];

  for (const { count, sides } of dice) {
    const absCount = Math.abs(count);
    const sign = count < 0 ? -1 : 1;
    for (let i = 0; i < absCount; i++) {
      rolls.push(sign * rollSingleDie(sides));
    }
  }

  const total = rolls.reduce((sum, r) => sum + r, 0) + modifier;

  return { formula, rolls, modifier, total };
}

// ============================================================================
// Hook
// ============================================================================

export interface UseDiceRollerReturn {
  roll: (formula: string) => DiceResult;
  lastResult: DiceResult | null;
  isRolling: boolean;
}

export function useDiceRoller(): UseDiceRollerReturn {
  const [lastResult, setLastResult] = useState<DiceResult | null>(null);
  const [isRolling, setIsRolling] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const roll = useCallback((formula: string): DiceResult => {
    // Clear any pending animation timer
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    setIsRolling(true);
    const result = rollDiceFromFormula(formula);
    setLastResult(result);

    timerRef.current = setTimeout(() => {
      setIsRolling(false);
      timerRef.current = null;
    }, 300);

    return result;
  }, []);

  return { roll, lastResult, isRolling };
}
