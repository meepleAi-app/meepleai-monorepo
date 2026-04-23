import { devWarnOnce } from '../hooks/devWarn';

import type { ConnectionChipProps, NavFooterItem } from '../types';

export function navItemsToConnections(items: NavFooterItem[]): ConnectionChipProps[] {
  return items.map((it, idx) => {
    const count = it.count ?? 0;
    let onCreate: (() => void) | undefined;
    if (it.showPlus && it.onPlusClick) {
      if (count === 0) {
        onCreate = it.onPlusClick;
      } else {
        devWarnOnce(
          `[MeepleCard adapter] navItems[${idx}].onPlusClick was dropped at count>0: ConnectionChip only exposes +create affordance when count=0. Move this handler to onCreate or gate it upstream.`
        );
      }
    }
    return {
      entityType: it.entity,
      label: it.label,
      count,
      href: it.href,
      disabled: it.disabled,
      onCreate,
      iconOverride: it.icon,
    };
  });
}
