/**
 * User Preferences Store
 * Issue #5523: User preferences with appMode casual/power
 *
 * Zustand store with localStorage persistence.
 * First consumer: appMode for RuleSourceCard display mode.
 */

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';

export type AppMode = 'casual' | 'power';

interface UserPreferencesState {
  appMode: AppMode;
  setAppMode: (mode: AppMode) => void;
}

export const useUserPreferences = create<UserPreferencesState>()(
  devtools(
    persist(
      set => ({
        appMode: 'casual',
        setAppMode: (mode: AppMode) => set({ appMode: mode }),
      }),
      {
        name: 'meepleai-user-prefs',
        partialize: state => ({ appMode: state.appMode }),
      }
    ),
    { name: 'UserPreferences' }
  )
);
