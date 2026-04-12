'use client';

import { useState } from 'react';

import type { InspectorEntry } from '@/dev-tools/types';

import { useStoreSlice } from '../hooks/useStoreSlice';

import type { RequestInspectorState } from '../stores/requestInspectorStore';
import type { StoreApi } from 'zustand/vanilla';

export interface InspectorSectionProps {
  inspectorStore: StoreApi<RequestInspectorState>;
}

type FilterType = 'all' | 'mock' | 'real';

const CELL_STYLE: React.CSSProperties = {
  padding: '4px 6px',
  fontSize: 10,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
};

function statusColor(status: number): string {
  if (status === 0) return '#ef4444'; // error
  if (status >= 500) return '#ef4444';
  if (status >= 400) return '#f59e0b';
  if (status >= 200 && status < 300) return '#22c55e';
  return '#9ca3af';
}

function methodColor(method: string): string {
  switch (method.toUpperCase()) {
    case 'GET':
      return '#60a5fa';
    case 'POST':
      return '#34d399';
    case 'PUT':
      return '#a78bfa';
    case 'PATCH':
      return '#fbbf24';
    case 'DELETE':
      return '#f87171';
    default:
      return '#9ca3af';
  }
}

function EntryRow({
  entry,
  isExpanded,
  onToggle,
}: {
  entry: InspectorEntry;
  isExpanded: boolean;
  onToggle: () => void;
}): React.JSX.Element {
  const time = new Date(entry.timestamp).toLocaleTimeString(undefined, {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });

  return (
    <>
      <div
        onClick={onToggle}
        role="row"
        data-testid={`inspector-row-${entry.id}`}
        aria-expanded={isExpanded}
        style={{
          display: 'grid',
          gridTemplateColumns: '50px 1fr 50px 55px 18px',
          cursor: 'pointer',
          background: isExpanded ? '#1f2937' : 'transparent',
          borderBottom: '1px solid #1f2937',
        }}
      >
        <div style={{ ...CELL_STYLE, color: methodColor(entry.method), fontWeight: 600 }}>
          {entry.method}
        </div>
        <div style={{ ...CELL_STYLE, color: '#d1d5db' }} title={entry.url}>
          {entry.url}
        </div>
        <div style={{ ...CELL_STYLE, color: statusColor(entry.status), textAlign: 'right' }}>
          {entry.status === 0 ? 'ERR' : entry.status}
        </div>
        <div style={{ ...CELL_STYLE, color: '#9ca3af', textAlign: 'right' }}>
          {entry.durationMs}ms
        </div>
        <div
          style={{
            ...CELL_STYLE,
            textAlign: 'center',
            color: entry.isMock ? '#f59e0b' : '#374151',
          }}
        >
          {entry.isMock ? 'M' : '·'}
        </div>
      </div>
      {isExpanded && (
        <div
          role="region"
          aria-label={`Details for ${entry.id}`}
          style={{
            padding: '8px 10px',
            background: '#111827',
            borderBottom: '1px solid #374151',
            fontSize: 10,
            color: '#9ca3af',
            display: 'flex',
            flexDirection: 'column',
            gap: 3,
          }}
        >
          <div>
            <span style={{ color: '#6b7280' }}>id: </span>
            <span>{entry.id}</span>
          </div>
          <div>
            <span style={{ color: '#6b7280' }}>time: </span>
            <span>{time}</span>
          </div>
          {entry.isMock && entry.mockSource && (
            <div>
              <span style={{ color: '#6b7280' }}>mock source: </span>
              <span style={{ color: '#f59e0b' }}>{entry.mockSource}</span>
            </div>
          )}
          <div>
            <span style={{ color: '#6b7280' }}>url: </span>
            <span style={{ color: '#d1d5db', wordBreak: 'break-all' }}>{entry.url}</span>
          </div>
        </div>
      )}
    </>
  );
}

export function InspectorSection({ inspectorStore }: InspectorSectionProps): React.JSX.Element {
  const entries = useStoreSlice(inspectorStore, s => s.entries);
  const [typeFilter, setTypeFilter] = useState<FilterType>('all');
  const [methodFilter, setMethodFilter] = useState<string>('all');
  const [expandedId, setExpandedId] = useState<string | null>(null);

  // Derive available methods from entries
  const availableMethods = Array.from(new Set(entries.map(e => e.method))).sort();

  const filtered = entries.filter(entry => {
    if (typeFilter === 'mock' && !entry.isMock) return false;
    if (typeFilter === 'real' && entry.isMock) return false;
    if (methodFilter !== 'all' && entry.method !== methodFilter) return false;
    return true;
  });

  const handleClear = () => {
    inspectorStore.getState().clear();
    setExpandedId(null);
  };

  return (
    <div
      data-testid="inspector-section"
      style={{ display: 'flex', flexDirection: 'column', gap: 8, height: '100%' }}
    >
      {/* Filters bar */}
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <select
          data-testid="inspector-type-filter"
          value={typeFilter}
          onChange={e => setTypeFilter(e.target.value as FilterType)}
          style={{
            background: '#1f2937',
            color: '#f9fafb',
            border: '1px solid #374151',
            borderRadius: 4,
            padding: '3px 6px',
            fontSize: 10,
            fontFamily: 'inherit',
          }}
        >
          <option value="all">All</option>
          <option value="mock">Mock</option>
          <option value="real">Real</option>
        </select>

        <select
          data-testid="inspector-method-filter"
          value={methodFilter}
          onChange={e => setMethodFilter(e.target.value)}
          style={{
            background: '#1f2937',
            color: '#f9fafb',
            border: '1px solid #374151',
            borderRadius: 4,
            padding: '3px 6px',
            fontSize: 10,
            fontFamily: 'inherit',
          }}
        >
          <option value="all">Method: All</option>
          {availableMethods.map(m => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>

        <button
          type="button"
          data-testid="inspector-clear"
          onClick={handleClear}
          style={{
            background: '#374151',
            color: '#f9fafb',
            border: 'none',
            borderRadius: 4,
            padding: '3px 8px',
            fontSize: 10,
            fontFamily: 'inherit',
            cursor: 'pointer',
            marginLeft: 'auto',
          }}
        >
          Clear
        </button>
      </div>

      {/* Count */}
      <div style={{ fontSize: 10, color: '#6b7280' }}>
        {filtered.length} request{filtered.length !== 1 ? 's' : ''}{' '}
        {filtered.length !== entries.length ? `(${entries.length} total)` : ''}
      </div>

      {/* Table */}
      {filtered.length === 0 ? (
        <div
          style={{ color: '#4b5563', fontSize: 11, textAlign: 'center', padding: '24px 0' }}
          data-testid="inspector-empty"
        >
          {entries.length === 0
            ? 'No requests captured yet. Requests will appear here as you use the app.'
            : 'No requests match the current filter.'}
        </div>
      ) : (
        <div
          role="table"
          aria-label="Captured HTTP requests"
          style={{
            border: '1px solid #1f2937',
            borderRadius: 4,
            overflow: 'auto',
            fontSize: 10,
          }}
        >
          {/* Header */}
          <div
            role="rowgroup"
            style={{
              display: 'grid',
              gridTemplateColumns: '50px 1fr 50px 55px 18px',
              background: '#1f2937',
              borderBottom: '1px solid #374151',
            }}
          >
            <div style={{ ...CELL_STYLE, color: '#6b7280' }}>Method</div>
            <div style={{ ...CELL_STYLE, color: '#6b7280' }}>URL</div>
            <div style={{ ...CELL_STYLE, color: '#6b7280', textAlign: 'right' }}>Status</div>
            <div style={{ ...CELL_STYLE, color: '#6b7280', textAlign: 'right' }}>Duration</div>
            <div style={{ ...CELL_STYLE, color: '#6b7280', textAlign: 'center' }}>M</div>
          </div>

          {filtered.map(entry => (
            <EntryRow
              key={entry.id}
              entry={entry}
              isExpanded={expandedId === entry.id}
              onToggle={() => setExpandedId(expandedId === entry.id ? null : entry.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
