import { createStore, type StoreApi } from 'zustand/vanilla';

import type { MockUser, UserRole } from './types';

export interface MockAuthState {
  currentUser: MockUser;
  availableUsers: MockUser[];
  setRole: (role: UserRole) => void;
  setUser: (user: MockUser) => void;
}

export interface MockAuthStoreInit {
  scenarioUser: MockUser;
  availableUsers: MockUser[];
  envRole: UserRole | null;
  queryStringRole: UserRole | null;
}

function findByRole(users: MockUser[], role: UserRole): MockUser | undefined {
  return users.find(u => u.role === role);
}

function resolveInitialUser(init: MockAuthStoreInit): MockUser {
  // Precedence: query string > env > scenario
  const requested = init.queryStringRole ?? init.envRole;
  if (requested) {
    const match = findByRole(init.availableUsers, requested);
    if (match) return match;
    // If scenario user itself has the requested role, use it
    if (init.scenarioUser.role === requested) return init.scenarioUser;
  }
  return init.scenarioUser;
}

export function createMockAuthStore(init: MockAuthStoreInit): StoreApi<MockAuthState> {
  return createStore<MockAuthState>((set, get) => ({
    currentUser: resolveInitialUser(init),
    availableUsers: init.availableUsers,
    setRole: (role: UserRole) => {
      const match = findByRole(get().availableUsers, role);
      if (match) {
        set({ currentUser: match });
      }
    },
    setUser: (user: MockUser) => set({ currentUser: user }),
  }));
}

/** Parse ?dev-role=Foo from URL (browser only). Returns null if not present or invalid. */
export function readRoleFromQueryString(): UserRole | null {
  if (typeof window === 'undefined') return null;
  const params = new URLSearchParams(window.location.search);
  const raw = params.get('dev-role');
  if (!raw) return null;
  const valid: UserRole[] = ['Guest', 'User', 'Editor', 'Admin', 'SuperAdmin'];
  return valid.includes(raw as UserRole) ? (raw as UserRole) : null;
}

/** Parse role from NEXT_PUBLIC_DEV_AS_ROLE env var. */
export function readRoleFromEnv(): UserRole | null {
  const raw = process.env.NEXT_PUBLIC_DEV_AS_ROLE;
  if (!raw) return null;
  const valid: UserRole[] = ['Guest', 'User', 'Editor', 'Admin', 'SuperAdmin'];
  return valid.includes(raw as UserRole) ? (raw as UserRole) : null;
}
