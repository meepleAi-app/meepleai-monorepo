/**
 * resolveSessionTools — Game toolkit override logic (Issue #4976).
 *
 * Determines which base tools remain visible and which custom tools appear
 * in the Tool Rail, given an optional published GameToolkit.
 *
 * Rules:
 * - Whiteboard is NEVER overridable (always visible).
 * - overridesTurnOrder → hides the base "turn-order" tool.
 * - overridesScoreboard → hides the base "scoreboard" tool.
 * - overridesDiceSet → hides the base "dice" tool.
 * - Each entry in diceTools/cardTools/timerTools/counterTools becomes a
 *   separate custom ToolItem shown under the "Custom" separator.
 */

import React from 'react';

import { Dices, Hash, PlaySquare, Timer } from 'lucide-react';

import type { ToolItem } from '@/components/session';

import type { GameToolkitDto } from '../types/gameToolkit';

// ── Constants ─────────────────────────────────────────────────────────────────

/** All base tool IDs that can potentially be overridden (except whiteboard). */
const OVERRIDABLE_BASE_TOOLS = new Set(['turn-order', 'scoreboard', 'dice']);

/** Complete set of all 4 base tool IDs. */
const ALL_BASE_TOOL_IDS: ReadonlySet<string> = new Set([
  'scoreboard',
  'turn-order',
  'dice',
  'whiteboard',
]);

// ── Output type ───────────────────────────────────────────────────────────────

export interface ResolvedTools {
  /**
   * Subset of ALL_BASE_TOOL_IDS that should remain visible.
   * Always includes 'whiteboard'; may exclude overridden tools.
   */
  visibleBaseToolIds: ReadonlySet<string>;
  /**
   * Custom ToolItems from the toolkit to display under the "Custom" separator.
   */
  customTools: ToolItem[];
}

// ── Utility ───────────────────────────────────────────────────────────────────

/**
 * Derive the set of visible base tools and the list of custom ToolItems.
 *
 * @param toolkit - The published GameToolkitDto for the session's game,
 *   or null/undefined when no toolkit is linked.
 */
export function resolveSessionTools(
  toolkit: GameToolkitDto | null | undefined,
): ResolvedTools {
  // No toolkit (or unpublished): show all 4 base tools, no custom tools.
  if (!toolkit || !toolkit.isPublished) {
    return { visibleBaseToolIds: ALL_BASE_TOOL_IDS, customTools: [] };
  }

  // Compute hidden base tools.
  const hiddenIds = new Set<string>();
  if (toolkit.overridesTurnOrder && OVERRIDABLE_BASE_TOOLS.has('turn-order')) {
    hiddenIds.add('turn-order');
  }
  if (toolkit.overridesScoreboard && OVERRIDABLE_BASE_TOOLS.has('scoreboard')) {
    hiddenIds.add('scoreboard');
  }
  if (toolkit.overridesDiceSet && OVERRIDABLE_BASE_TOOLS.has('dice')) {
    hiddenIds.add('dice');
  }

  const visibleBaseToolIds = new Set(
    [...ALL_BASE_TOOL_IDS].filter(id => !hiddenIds.has(id)),
  );

  // Map extra tools → ToolItem[].
  const customTools: ToolItem[] = [];

  toolkit.diceTools.forEach((dice, i) => {
    customTools.push({
      id: `custom-dice-${i}`,
      label: dice.name,
      shortLabel: dice.name.slice(0, 6),
      icon: <Dices className="w-5 h-5" aria-hidden="true" />,
      type: 'custom',
    });
  });

  toolkit.cardTools.forEach((card, i) => {
    customTools.push({
      id: `custom-card-${i}`,
      label: card.name,
      shortLabel: card.name.slice(0, 6),
      icon: <PlaySquare className="w-5 h-5" aria-hidden="true" />,
      type: 'custom',
    });
  });

  toolkit.timerTools.forEach((timer, i) => {
    customTools.push({
      id: `custom-timer-${i}`,
      label: timer.name,
      shortLabel: timer.name.slice(0, 6),
      icon: <Timer className="w-5 h-5" aria-hidden="true" />,
      type: 'custom',
    });
  });

  toolkit.counterTools.forEach((counter, i) => {
    customTools.push({
      id: `custom-counter-${i}`,
      label: counter.name,
      shortLabel: counter.name.slice(0, 6),
      icon: <Hash className="w-5 h-5" aria-hidden="true" />,
      type: 'custom',
    });
  });

  return { visibleBaseToolIds, customTools };
}
