'use client';

import { memo, useEffect, useRef } from 'react';

import { CompactCard } from './variants/CompactCard';
import { FeaturedCard } from './variants/FeaturedCard';
import { FocusCard } from './variants/FocusCard';
import { GridCard } from './variants/GridCard';
import { HeroCard } from './variants/HeroCard';
import { ListCard } from './variants/ListCard';

import type { MeepleCardProps } from './types';

const variantMap = {
  grid: GridCard,
  list: ListCard,
  compact: CompactCard,
  featured: FeaturedCard,
  hero: HeroCard,
  focus: FocusCard,
} as const;

/**
 * Dedup registry for the W1 `navItems` deprecation warning.
 * Keyed by a per-instance anchor object (via `useRef({})`), so each mounted
 * MeepleCard emits the warning at most once regardless of re-render count.
 * Keeping the WeakSet keyed by ref (not by props, which is fresh each render)
 * is essential: tests and call sites frequently spread/rebuild props, which
 * would defeat a props-based key.
 */
const deprecationWarned = new WeakSet<object>();

function emitNavItemsDeprecation(instanceKey: object): void {
  if (process.env.NODE_ENV === 'production') return;
  if (deprecationWarned.has(instanceKey)) return;
  deprecationWarned.add(instanceKey);
  console.warn(
    '[MeepleCard] The `navItems` prop is deprecated. Migrate to `connections` by 2026-07-15.\n' +
      '  See docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md\n' +
      '  This warning is shown once per MeepleCard instance in development mode.'
  );
}

/**
 * Test-only helper. WeakSet entries auto-collect when the keyed anchor is
 * unreachable, so in practice cross-test isolation is provided by the fact
 * that each test's `render()` mints a fresh `useRef({})` anchor. This helper
 * is a no-op today, kept as an explicit reset hook so tests can signal intent
 * and so a future migration to `Set<WeakRef>` or similar has a stable API.
 */
export function __resetDeprecationDedup(): void {
  // intentional no-op — see doc above
}

function MeepleCardImpl(props: MeepleCardProps) {
  const instanceKey = useRef<object>({});

  useEffect(() => {
    if (props.navItems && props.navItems.length > 0 && props.connections === undefined) {
      emitNavItemsDeprecation(instanceKey.current);
    }
  }, [props.navItems, props.connections]);

  const variant = props.variant ?? 'grid';
  const Renderer = variantMap[variant];
  return <Renderer {...props} />;
}

export const MeepleCard = memo(MeepleCardImpl);
