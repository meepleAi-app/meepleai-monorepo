/**
 * GameDetailTabsAnimated - thin consumer over the v2 primitive
 * (`AnimatedUnderlineTabs`) after the #2311 DEC-D5 extraction.
 *
 * Preserves the original public surface (props + `tabIdFor` / `panelIdFor`
 * helpers) so existing call sites + tests keep working. The primitive owns
 * the visuals + a11y wiring + reduced-motion animation; this wrapper only
 * sets `slotPrefix='game-detail-tab'` to keep stable selectors.
 *
 * Refs: feat/v2 Wave C.1 (Issue #581), #2311 DEC-D5.
 */

'use client';

import { type ReactElement } from 'react';

import {
  AnimatedUnderlineTabs,
  type AnimatedUnderlineTabConfig,
  type AnimatedUnderlineTabsProps,
} from '@/components/ui/navigation/animated-underline-tabs';

export type TabKey = 'info' | 'rules' | 'faqs' | 'sessions' | 'stats' | 'agents' | 'documents';

export type GameDetailTabConfig<TKey extends string = string> = AnimatedUnderlineTabConfig<TKey>;

export type GameDetailTabsAnimatedProps<TKey extends string = string> = Omit<
  AnimatedUnderlineTabsProps<TKey>,
  'slotPrefix' | 'accentActiveClassName' | 'accentIndicatorClassName' | 'accentCountActiveClassName'
>;

/**
 * Returns the HTML `id` for a game-detail tab button.
 * Orchestrator and panels use this to wire `aria-labelledby`.
 */
export function tabIdFor(key: string): string {
  return `game-detail-tab-${key}`;
}

/**
 * Returns the HTML `id` for a game-detail tab panel.
 * Orchestrator uses this as `id` on the panel element + `aria-controls` on the button.
 */
export function panelIdFor(key: string): string {
  return `game-detail-panel-${key}`;
}

export function GameDetailTabsAnimated<TKey extends string = string>(
  props: GameDetailTabsAnimatedProps<TKey>
): ReactElement {
  return <AnimatedUnderlineTabs<TKey> {...props} slotPrefix="game-detail-tab" />;
}
