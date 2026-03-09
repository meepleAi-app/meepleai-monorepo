/**
 * useAppMode Hook
 * Issue #5523: Lightweight wrapper over user preferences store.
 */

import { useUserPreferences, type AppMode } from '@/lib/stores/user-preferences';

export function useAppMode(): AppMode {
  return useUserPreferences(s => s.appMode);
}
