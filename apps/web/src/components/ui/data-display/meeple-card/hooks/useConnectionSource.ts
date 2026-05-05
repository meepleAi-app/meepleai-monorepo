import type { ConnectionChipProps, MeepleCardProps } from '../types';

type Source = 'connections' | 'manaPips' | null;

export interface UseConnectionSourceResult {
  source: Source;
  items: ConnectionChipProps[];
  variant: 'footer' | 'inline';
}

export function useConnectionSource(
  props: Pick<MeepleCardProps, 'connections' | 'connectionsVariant' | 'manaPips'>
): UseConnectionSourceResult {
  const variant = props.connectionsVariant === 'inline' ? 'inline' : 'footer';

  // Precedence: `connections` always wins (even when empty array, which means
  // an explicit "no connections"). `manaPips` is the legacy fallback path.
  if (props.connections !== undefined) {
    return { source: 'connections', items: props.connections, variant };
  }

  if (props.manaPips !== undefined) {
    return { source: 'manaPips', items: [], variant };
  }

  return { source: null, items: [], variant };
}
