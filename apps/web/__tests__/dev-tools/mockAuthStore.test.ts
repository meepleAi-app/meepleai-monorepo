import { describe, it, expect } from 'vitest';
import { createMockAuthStore } from '@/dev-tools/mockAuthStore';
import type { MockUser } from '@/dev-tools/types';

const USERS: MockUser[] = [
  { id: 'MOCK-admin', email: 'a@m.local', displayName: 'Admin', role: 'Admin' },
  { id: 'MOCK-user', email: 'u@m.local', displayName: 'User', role: 'User' },
  { id: 'MOCK-guest', email: 'g@m.local', displayName: 'Guest', role: 'Guest' },
];

describe('mockAuthStore', () => {
  it('uses currentUser from scenario when no override', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: null,
      queryStringRole: null,
    });
    expect(store.getState().currentUser.id).toBe('MOCK-admin');
  });

  it('env var DEV_AS_ROLE overrides scenario', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'User',
      queryStringRole: null,
    });
    expect(store.getState().currentUser.role).toBe('User');
    expect(store.getState().currentUser.id).toBe('MOCK-user');
  });

  it('query string ?dev-role wins over env', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'User',
      queryStringRole: 'Guest',
    });
    expect(store.getState().currentUser.role).toBe('Guest');
  });

  it('falls back to scenario user if requested role not available', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: 'SuperAdmin',
      queryStringRole: null,
    });
    // No SuperAdmin in availableUsers → fall back to scenario
    expect(store.getState().currentUser.id).toBe('MOCK-admin');
  });

  it('setRole updates current user if role available', () => {
    const store = createMockAuthStore({
      scenarioUser: USERS[0],
      availableUsers: USERS,
      envRole: null,
      queryStringRole: null,
    });
    store.getState().setRole('User');
    expect(store.getState().currentUser.role).toBe('User');
  });
});
