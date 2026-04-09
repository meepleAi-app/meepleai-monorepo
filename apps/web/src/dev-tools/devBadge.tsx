'use client';

import { useEffect, useState } from 'react';

import type { MockAuthState } from './mockAuthStore';
import type { MockControlState } from './mockControlCore';
import type { ScenarioState } from './scenarioStore';
import type { StoreApi } from 'zustand/vanilla';

export interface DevBadgeProps {
  controlStore: StoreApi<MockControlState>;
  scenarioStore: StoreApi<ScenarioState>;
  authStore: StoreApi<MockAuthState>;
}

function useStoreSlice<T, U>(store: StoreApi<T>, selector: (state: T) => U): U {
  const [slice, setSlice] = useState<U>(() => selector(store.getState()));
  useEffect(() => {
    return store.subscribe(state => {
      setSlice(selector(state));
    });
  }, [store, selector]);
  return slice;
}

export function DevBadge({ controlStore, scenarioStore, authStore }: DevBadgeProps) {
  const groups = useStoreSlice(controlStore, s => s.toggles.groups);
  const scenarioName = useStoreSlice(scenarioStore, s => s.scenario.name);
  const role = useStoreSlice(authStore, s => s.currentUser.role);

  const [backendHealthy, setBackendHealthy] = useState<boolean | null>(null);

  // Health polling for backend (only when MOCK_MODE is off)
  useEffect(() => {
    const expectBackend = process.env.NEXT_PUBLIC_MOCK_MODE !== 'true';
    if (!expectBackend) return;

    let failCount = 0;
    const interval = setInterval(async () => {
      try {
        const res = await fetch('/health', { cache: 'no-store' });
        if (res.ok) {
          failCount = 0;
          setBackendHealthy(true);
        } else {
          failCount++;
        }
      } catch {
        failCount++;
      }
      if (failCount >= 5) {
        setBackendHealthy(false);
      }
    }, 10_000);

    return () => clearInterval(interval);
  }, []);

  const enabledCount = Object.values(groups).filter(Boolean).length;
  const totalCount = Object.keys(groups).length;
  const allMocked = enabledCount === totalCount && totalCount > 0;
  const noneMocked = enabledCount === 0;

  const baseColor = noneMocked ? '#22c55e' : allMocked ? '#ef4444' : '#f59e0b';
  const backendWarn = backendHealthy === false;
  const color = backendWarn ? '#ef4444' : baseColor;

  const badgeText = backendWarn
    ? `\u26a0 BACKEND DOWN \u00b7 ${scenarioName}`
    : `MOCK \u00b7 ${enabledCount}/${totalCount} \u00b7 ${scenarioName} \u00b7 ${role}`;

  return (
    <div
      data-testid="dev-badge"
      style={{
        position: 'fixed',
        bottom: 12,
        right: 12,
        zIndex: 99999,
        background: '#111827',
        color: '#f9fafb',
        padding: '8px 12px',
        borderRadius: 8,
        fontFamily: 'monospace',
        fontSize: 12,
        boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
        border: `2px solid ${color}`,
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        pointerEvents: 'auto',
      }}
      title={`MeepleDev: ${enabledCount}/${totalCount} groups mocked \u00b7 scenario: ${scenarioName} \u00b7 role: ${role}`}
    >
      <span
        style={{
          width: 10,
          height: 10,
          borderRadius: '50%',
          background: color,
          display: 'inline-block',
        }}
      />
      <span>{badgeText}</span>
    </div>
  );
}
