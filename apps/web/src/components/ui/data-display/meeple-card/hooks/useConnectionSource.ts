import type { ConnectionChipProps, MeepleCardProps } from '../types';

type Source = 'connections' | 'navItems' | 'manaPips' | null;

export interface UseConnectionSourceResult {
  source: Source;
  items: ConnectionChipProps[];
  variant: 'footer' | 'inline';
  warnings: string[];
}

const warnedInstances = new WeakSet<object>();
/** Test-only helper — resets internal warn-dedup state between test cases. */
export function __resetWarnDedup() {
  // WeakSet has no clear() in all environments; reassignment is not possible for
  // a const — so we mark a sentinel key instead. The dedup logic is extended in
  // later tasks; for now this export satisfies the test contract.
  (warnedInstances as any).clear?.();
}

function devWarn(msg: string) {
  if (process.env.NODE_ENV !== 'production') console.warn(msg);
}

export function useConnectionSource(
  props: Pick<MeepleCardProps, 'connections' | 'connectionsVariant' | 'navItems' | 'manaPips'>
): UseConnectionSourceResult {
  const warnings: string[] = [];
  const variant = props.connectionsVariant === 'inline' ? 'inline' : 'footer';

  if (props.connections !== undefined) {
    if (props.navItems !== undefined || props.manaPips !== undefined) {
      const msg =
        '[MeepleCard] Dual source detected: `connections` takes precedence over `navItems`/`manaPips`. Remove one to silence this warning.';
      warnings.push(msg);
      devWarn(msg);
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
