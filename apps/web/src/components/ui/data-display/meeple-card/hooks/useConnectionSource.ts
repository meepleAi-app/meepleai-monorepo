import { devWarnOnce } from './devWarn';

import type { ConnectionChipProps, MeepleCardProps } from '../types';

type Source = 'connections' | 'navItems' | 'manaPips' | null;

export interface UseConnectionSourceResult {
  source: Source;
  items: ConnectionChipProps[];
  variant: 'footer' | 'inline';
  warnings: string[];
}

export function useConnectionSource(
  props: Pick<MeepleCardProps, 'connections' | 'connectionsVariant' | 'navItems' | 'manaPips'>
): UseConnectionSourceResult {
  const warnings: string[] = [];
  const variant = props.connectionsVariant === 'inline' ? 'inline' : 'footer';

  // Precedence per spec R1.6.3: `connections` takes priority even when empty
  // (`connections: []` is an explicit "none"). `navItems` falls through when empty
  // to preserve legacy null semantics. Do NOT unify these guards.
  if (props.connections !== undefined) {
    if (props.navItems !== undefined || props.manaPips !== undefined) {
      const msg =
        '[MeepleCard] Dual source detected: `connections` takes precedence over `navItems`/`manaPips`. Remove one to silence this warning.';
      warnings.push(msg);
      devWarnOnce(msg);
    }
    return { source: 'connections', items: props.connections, variant, warnings };
  }

  if (props.navItems && props.navItems.length > 0) {
    return { source: 'navItems', items: [], variant, warnings };
  }

  if (props.manaPips !== undefined) {
    return { source: 'manaPips', items: [], variant, warnings };
  }

  return { source: null, items: [], variant, warnings };
}
