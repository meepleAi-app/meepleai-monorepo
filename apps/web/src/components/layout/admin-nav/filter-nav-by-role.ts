import type { AuthUser } from '@/types/auth';
import { hasRole } from '@/types/auth';

import type { AdminNavGroup } from './admin-nav-config';

const DEFAULT_MIN_ROLE = 'admin' as const;

/**
 * Returns a copy of `groups` keeping only the items the user is allowed to see
 * (per item `minRole`, defaulting to 'admin'), and dropping groups left empty.
 * Pure: does not mutate the input.
 */
export function filterNavByRole(groups: AdminNavGroup[], user: AuthUser | null): AdminNavGroup[] {
  return groups
    .map(group => ({
      ...group,
      items: group.items.filter(item => hasRole(user, item.minRole ?? DEFAULT_MIN_ROLE)),
    }))
    .filter(group => group.items.length > 0);
}
