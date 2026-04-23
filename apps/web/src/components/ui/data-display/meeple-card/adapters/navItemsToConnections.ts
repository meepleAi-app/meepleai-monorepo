import type { ConnectionChipProps, NavFooterItem } from '../types';

export function navItemsToConnections(items: NavFooterItem[]): ConnectionChipProps[] {
  return items.map(it => ({
    entityType: it.entity,
    label: it.label,
    count: it.count ?? 0,
    href: it.href,
    disabled: it.disabled,
    iconOverride: it.icon,
  }));
}
