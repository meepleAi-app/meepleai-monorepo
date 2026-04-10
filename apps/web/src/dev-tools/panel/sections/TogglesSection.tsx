'use client';

import { useEffect, useMemo, useCallback } from 'react';

import { ToggleSwitch } from '../components/ToggleSwitch';
import { useBackendTogglesMutation } from '../hooks/useBackendTogglesMutation';
import { useBackendTogglesQuery } from '../hooks/useBackendTogglesQuery';
import { useStoreSlice } from '../hooks/useStoreSlice';

import type { StoreApi } from 'zustand/vanilla';

export interface MockControlState {
  toggles: { groups: Record<string, boolean>; overrides: Record<string, boolean> };
  setGroup: (name: string, value: boolean) => void;
}

export interface HandlerGroup {
  name: string;
  handlers: unknown[];
}

export interface TogglesSectionProps {
  mockControlStore: StoreApi<MockControlState>;
  handlerGroups: HandlerGroup[];
  worker: { resetHandlers: (...handlers: unknown[]) => void };
}

export function TogglesSection({
  mockControlStore,
  handlerGroups,
  worker,
}: TogglesSectionProps): React.JSX.Element {
  const groups = useStoreSlice(mockControlStore, s => s.toggles.groups);
  const query = useBackendTogglesQuery();
  const mutation = useBackendTogglesMutation();

  const applyMswHandlers = useCallback(() => {
    const active = handlerGroups.flatMap(g => (groups[g.name] !== false ? g.handlers : []));
    worker.resetHandlers(...active);
  }, [groups, handlerGroups, worker]);

  useEffect(() => {
    applyMswHandlers();
  }, [applyMswHandlers]);

  const handleMswToggle = (groupName: string, value: boolean): void => {
    mockControlStore.getState().setGroup(groupName, value);
  };

  const handleBackendToggle = async (serviceName: string, value: boolean): Promise<void> => {
    try {
      await mutation.setToggle(serviceName, value);
      await query.refetch();
    } catch {
      /* surfaced via error state */
    }
  };

  const handleResetMsw = (): void => {
    handlerGroups.forEach(g => mockControlStore.getState().setGroup(g.name, true));
  };

  const handleResetBackend = async (): Promise<void> => {
    try {
      await mutation.resetAll();
      await query.refetch();
    } catch {
      /* surfaced */
    }
  };

  const sortedGroups = useMemo(() => handlerGroups.map(g => g.name).sort(), [handlerGroups]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <section>
        <header
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 8,
          }}
        >
          <h3
            style={{
              fontSize: 11,
              color: '#f59e0b',
              textTransform: 'uppercase',
              letterSpacing: 0.5,
              margin: 0,
            }}
          >
            Frontend (MSW) — {sortedGroups.length} groups
          </h3>
          <button
            type="button"
            onClick={handleResetMsw}
            data-testid="toggles-reset-msw"
            style={{
              fontSize: 10,
              padding: '4px 8px',
              background: 'transparent',
              color: '#9ca3af',
              border: '1px solid #374151',
              borderRadius: 4,
              cursor: 'pointer',
            }}
          >
            Reset all
          </button>
        </header>
        {sortedGroups.map(groupName => (
          <ToggleSwitch
            key={`msw-${groupName}`}
            checked={groups[groupName] !== false}
            onChange={next => handleMswToggle(groupName, next)}
            label={groupName}
            testId={`toggle-msw-${groupName}`}
          />
        ))}
      </section>

      <section>
        <header
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 8,
          }}
        >
          <h3
            style={{
              fontSize: 11,
              color: '#f59e0b',
              textTransform: 'uppercase',
              letterSpacing: 0.5,
              margin: 0,
            }}
          >
            Backend services — {query.knownServices.length}
          </h3>
          {query.knownServices.length > 0 ? (
            <button
              type="button"
              onClick={handleResetBackend}
              data-testid="toggles-reset-backend"
              disabled={mutation.isMutating}
              style={{
                fontSize: 10,
                padding: '4px 8px',
                background: 'transparent',
                color: '#9ca3af',
                border: '1px solid #374151',
                borderRadius: 4,
                cursor: mutation.isMutating ? 'not-allowed' : 'pointer',
              }}
            >
              Reset all
            </button>
          ) : null}
        </header>

        {query.error ? (
          <div
            role="status"
            style={{
              padding: 12,
              background: '#7f1d1d',
              color: '#fecaca',
              borderRadius: 6,
              fontSize: 11,
              marginBottom: 8,
            }}
          >
            <strong>Backend unreachable</strong>
            <div style={{ marginTop: 4 }}>{query.error.message}</div>
            <button
              type="button"
              onClick={() => void query.refetch()}
              style={{
                marginTop: 6,
                padding: '4px 8px',
                background: '#f9fafb',
                color: '#7f1d1d',
                border: 'none',
                borderRadius: 4,
                cursor: 'pointer',
                fontSize: 10,
              }}
            >
              Retry
            </button>
          </div>
        ) : null}

        {query.isLoading && !query.knownServices.length ? (
          <div style={{ fontSize: 11, color: '#9ca3af', padding: '8px 12px' }}>Loading...</div>
        ) : null}

        {query.knownServices.map(serviceName => (
          <ToggleSwitch
            key={`be-${serviceName}`}
            checked={query.toggles[serviceName] === true}
            onChange={next => void handleBackendToggle(serviceName, next)}
            label={serviceName}
            disabled={mutation.isMutating}
            testId={`toggle-be-${serviceName}`}
          />
        ))}
      </section>
    </div>
  );
}
