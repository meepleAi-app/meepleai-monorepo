'use client';

import type { DevPanelTab } from '@/dev-tools/types';

import { SectionErrorBoundary } from './components/SectionErrorBoundary';
import { useStoreSlice } from './hooks/useStoreSlice';
import { TogglesSection } from './sections/TogglesSection';

import type { MockControlState, HandlerGroup } from './sections/TogglesSection';
import type { PanelUiState } from './stores/panelUiStore';
import type { StoreApi } from 'zustand/vanilla';

export interface DevPanelProps {
  uiStore: StoreApi<PanelUiState>;
  mockControlStore: StoreApi<MockControlState>;
  handlerGroups: HandlerGroup[];
  worker: { resetHandlers: (...handlers: unknown[]) => void };
}

const TABS: { id: DevPanelTab; label: string }[] = [
  { id: 'toggles', label: 'Toggles' },
  { id: 'scenarios', label: 'Scenarios' },
  { id: 'auth', label: 'Auth' },
  { id: 'inspector', label: 'Inspector' },
];

export function DevPanel({
  uiStore,
  mockControlStore,
  handlerGroups,
  worker,
}: DevPanelProps): React.JSX.Element | null {
  const isOpen = useStoreSlice(uiStore, s => s.isOpen);
  const activeTab = useStoreSlice(uiStore, s => s.activeTab);
  const drawerWidth = useStoreSlice(uiStore, s => s.drawerWidth);

  if (!isOpen) return null;

  return (
    <div
      data-testid="dev-panel"
      role="dialog"
      aria-modal="false"
      aria-label="MeepleDev Panel"
      style={{
        position: 'fixed',
        top: 0,
        right: 0,
        bottom: 0,
        width: drawerWidth,
        background: '#111827',
        color: '#f9fafb',
        boxShadow: '-4px 0 24px rgba(0,0,0,0.5)',
        zIndex: 99998,
        display: 'flex',
        flexDirection: 'column',
        fontFamily: 'monospace',
      }}
    >
      <header
        style={{
          padding: '12px 16px',
          borderBottom: '1px solid #374151',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <strong style={{ fontSize: 13, color: '#f59e0b' }}>MeepleDev Panel</strong>
        <button
          type="button"
          onClick={() => uiStore.getState().setOpen(false)}
          aria-label="Close Dev Panel"
          style={{
            background: 'transparent',
            color: '#f9fafb',
            border: 'none',
            cursor: 'pointer',
            fontSize: 16,
          }}
        >
          ×
        </button>
      </header>
      <div
        role="tablist"
        aria-label="Dev Panel sections"
        style={{ display: 'flex', borderBottom: '1px solid #374151' }}
      >
        {TABS.map(tab => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.id}
            data-testid={`panel-tab-${tab.id}`}
            onClick={() => uiStore.getState().setActiveTab(tab.id)}
            style={{
              flex: 1,
              padding: '8px 12px',
              background: activeTab === tab.id ? '#1f2937' : 'transparent',
              color: activeTab === tab.id ? '#f59e0b' : '#9ca3af',
              border: 'none',
              borderBottom: activeTab === tab.id ? '2px solid #f59e0b' : '2px solid transparent',
              cursor: 'pointer',
              fontFamily: 'inherit',
              fontSize: 11,
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div
        role="tabpanel"
        aria-labelledby={`panel-tab-${activeTab}`}
        style={{ flex: 1, padding: 16, overflow: 'auto' }}
      >
        {activeTab === 'toggles' ? (
          <SectionErrorBoundary sectionName="Toggles">
            <TogglesSection
              mockControlStore={mockControlStore}
              handlerGroups={handlerGroups}
              worker={worker}
            />
          </SectionErrorBoundary>
        ) : (
          <p style={{ color: '#9ca3af', fontSize: 12 }}>
            [{activeTab}] section content — coming in M
            {activeTab === 'scenarios' || activeTab === 'auth' ? '2' : '3'}
          </p>
        )}
      </div>
    </div>
  );
}
