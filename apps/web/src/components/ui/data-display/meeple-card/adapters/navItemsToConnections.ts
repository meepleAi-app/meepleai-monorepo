import type { ConnectionChipProps, NavFooterItem } from '../types';

export function navItemsToConnections(items: NavFooterItem[]): ConnectionChipProps[] {
  return items.map(it => {
    let onCreate: (() => void) | undefined;
    if (it.showPlus && it.onPlusClick && (it.count ?? 0) === 0) {
      onCreate = it.onPlusClick;
    }
    return {
      entityType: it.entity,
      label: it.label,
      count: it.count ?? 0,
      href: it.href,
      disabled: it.disabled,
      onCreate,
      iconOverride: it.icon,
    };
  });
}
