'use client';

import { useState } from 'react';

import type { MockAuthState } from '@/dev-tools/mockAuthStore';
import { SCENARIO_MANIFEST } from '@/dev-tools/scenarioManifest';
import type { ScenarioState } from '@/dev-tools/scenarioStore';
import { switchScenario } from '@/dev-tools/scenarioSwitcher';

import { useStoreSlice } from '../hooks/useStoreSlice';

import type { QueryClient } from '@tanstack/react-query';
import type { StoreApi } from 'zustand/vanilla';

export interface ScenariosSectionProps {
  scenarioStore: StoreApi<ScenarioState>;
  authStore: StoreApi<MockAuthState>;
  queryClient: QueryClient;
}

export function ScenariosSection({
  scenarioStore,
  authStore,
  queryClient,
}: ScenariosSectionProps): React.JSX.Element {
  const currentScenario = useStoreSlice(scenarioStore, s => s.scenario);
  const isSwitching = useStoreSlice(scenarioStore, s => s.isSwitching);
  const [error, setError] = useState<string | null>(null);
  const scenarios = Object.keys(SCENARIO_MANIFEST).sort();

  const handleChange = async (newName: string): Promise<void> => {
    if (newName === currentScenario.name) return;
    setError(null);
    try {
      await switchScenario(newName, { scenarioStore, authStore, queryClient });
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
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
          Active scenario
        </h3>
        <div style={{ fontSize: 12, color: '#f9fafb', marginBottom: 4 }}>
          {currentScenario.name}
        </div>
        <div style={{ fontSize: 10, color: '#9ca3af' }}>{currentScenario.description}</div>
      </section>
      <section>
        <label
          htmlFor="scenario-select"
          style={{ fontSize: 11, color: '#9ca3af', display: 'block', marginBottom: 4 }}
        >
          Switch to:
        </label>
        <select
          id="scenario-select"
          data-testid="scenario-select"
          value={currentScenario.name}
          disabled={isSwitching}
          onChange={e => void handleChange(e.target.value)}
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
          {scenarios.map(name => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
        {isSwitching ? (
          <div style={{ fontSize: 10, color: '#f59e0b', marginTop: 6 }}>Switching...</div>
        ) : null}
        {error ? (
          <div role="alert" style={{ fontSize: 10, color: '#fecaca', marginTop: 6 }}>
            {error}
          </div>
        ) : null}
      </section>
    </div>
  );
}
