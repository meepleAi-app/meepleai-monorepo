'use client';

import type { MockAuthState } from '@/dev-tools/mockAuthStore';
import type { UserRole } from '@/dev-tools/types';

import { useStoreSlice } from '../hooks/useStoreSlice';

import type { QueryClient } from '@tanstack/react-query';
import type { StoreApi } from 'zustand/vanilla';

export interface AuthSectionProps {
  authStore: StoreApi<MockAuthState>;
  queryClient: QueryClient;
}

export function AuthSection({ authStore, queryClient }: AuthSectionProps): React.JSX.Element {
  const currentUser = useStoreSlice(authStore, s => s.currentUser);
  const availableUsers = useStoreSlice(authStore, s => s.availableUsers);

  const handleRoleChange = async (newRole: string): Promise<void> => {
    authStore.getState().setRole(newRole as UserRole);
    await queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <section>
        <h3
          style={{
            fontSize: 11,
            color: '#f59e0b',
            textTransform: 'uppercase',
            letterSpacing: 0.5,
            marginTop: 0,
            marginBottom: 8,
          }}
        >
          Current user
        </h3>
        <div style={{ fontSize: 12, color: '#f9fafb' }}>{currentUser.displayName}</div>
        <div style={{ fontSize: 10, color: '#9ca3af', marginTop: 2 }}>
          {currentUser.email} · <strong>{currentUser.role}</strong>
        </div>
      </section>
      <section>
        <label
          htmlFor="role-select"
          style={{ fontSize: 11, color: '#9ca3af', display: 'block', marginBottom: 4 }}
        >
          Switch role:
        </label>
        <select
          id="role-select"
          data-testid="role-select"
          value={currentUser.role}
          onChange={e => void handleRoleChange(e.target.value)}
          style={{
            width: '100%',
            padding: '6px 8px',
            background: '#1f2937',
            color: '#f9fafb',
            border: '1px solid #374151',
            borderRadius: 4,
            fontSize: 12,
            fontFamily: 'inherit',
          }}
        >
          {availableUsers.map(u => (
            <option key={u.id} value={u.role}>
              {u.role} ({u.displayName})
            </option>
          ))}
        </select>
      </section>
    </div>
  );
}
