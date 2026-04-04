'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import {
  useGameAgentDocuments,
  useUpdateGameAgentDocuments,
} from '@/hooks/queries/useGameAgentDocuments';
import type { DocumentSelectionItem } from '@/lib/api/schemas/agent-documents.schemas';

// ========== Types ==========

interface DocumentSelectionPanelProps {
  gameId: string;
  onSelectionChange?: (selectedIds: string[]) => void;
  initialSelection?: string[];
  readOnly?: boolean;
  wizardMode?: boolean;
}

// ========== Helpers ==========

function statusLabel(state: string): { text: string; color: string; pulse: boolean } {
  switch (state.toLowerCase()) {
    case 'completed':
    case 'ready':
      return { text: 'Ready', color: 'bg-green-500', pulse: false };
    case 'processing':
    case 'in_progress':
      return { text: 'In elaborazione', color: 'bg-amber-500', pulse: true };
    case 'failed':
    case 'error':
      return { text: 'Fallito', color: 'bg-red-500', pulse: false };
    default:
      return { text: state, color: 'bg-gray-500', pulse: false };
  }
}

// ========== Sub-components ==========

function StatusBadge({ state }: { state: string }) {
  const { text, color, pulse } = statusLabel(state);
  return (
    <span className="inline-flex items-center gap-1 text-xs text-gray-400">
      <span className={`h-1.5 w-1.5 rounded-full ${color} ${pulse ? 'animate-pulse' : ''}`} />
      {text}
    </span>
  );
}

function DocumentRow({
  doc,
  inputType,
  checked,
  onChange,
  readOnly,
  name,
}: {
  doc: DocumentSelectionItem;
  inputType: 'radio' | 'checkbox';
  checked: boolean;
  onChange: (id: string, selected: boolean) => void;
  readOnly: boolean;
  name?: string;
}) {
  const inputId = `doc-${doc.documentId}`;
  return (
    <label
      htmlFor={inputId}
      className={`flex items-center gap-3 rounded-md border px-3 py-2 transition-colors
        ${checked ? 'border-amber-500/40 bg-amber-500/5' : 'border-[#30363d] bg-[#21262d]'}
        ${readOnly ? 'cursor-default opacity-70' : 'cursor-pointer hover:border-[#484f58]'}`}
    >
      <input
        id={inputId}
        type={inputType}
        name={name}
        checked={checked}
        disabled={readOnly}
        onChange={() => onChange(doc.documentId, !checked)}
        className="accent-amber-500 h-4 w-4 shrink-0"
      />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm text-gray-200">{doc.fileName}</span>
          {doc.isPrivate && (
            <span className="shrink-0 rounded bg-violet-500/20 px-1.5 py-0.5 text-[10px] text-violet-300">
              (privato)
            </span>
          )}
        </div>
        <div className="mt-0.5 flex items-center gap-2">
          <span className="text-xs text-gray-500">{doc.documentType}</span>
          {doc.pageCount != null && (
            <span className="text-xs text-gray-500">{doc.pageCount} pag.</span>
          )}
        </div>
      </div>
      <StatusBadge state={doc.processingState} />
    </label>
  );
}

function LoadingSkeleton() {
  return (
    <div className="space-y-3">
      {[1, 2, 3].map(i => (
        <div
          key={i}
          className="h-14 animate-pulse rounded-md border border-[#30363d] bg-[#21262d]"
        />
      ))}
    </div>
  );
}

// ========== Main Component ==========

export function DocumentSelectionPanel({
  gameId,
  onSelectionChange,
  initialSelection,
  readOnly = false,
  wizardMode = false,
}: DocumentSelectionPanelProps) {
  const { data, isLoading } = useGameAgentDocuments(gameId);
  const mutation = useUpdateGameAgentDocuments(gameId);

  // Local selection state -- seeded from server data or initialSelection
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set(initialSelection ?? []));
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const initializedRef = useRef(false);

  // Seed from server once data arrives (only if no initialSelection was provided)
  useEffect(() => {
    if (data && !initializedRef.current) {
      initializedRef.current = true;
      if (!initialSelection) {
        const serverSelected = new Set<string>();
        for (const d of data.baseDocuments) if (d.isSelected) serverSelected.add(d.documentId);
        for (const d of data.additionalDocuments)
          if (d.isSelected) serverSelected.add(d.documentId);
        setSelectedIds(serverSelected);
      }
    }
  }, [data, initialSelection]);

  // Cleanup debounce timer
  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  // Persist selection (debounced, non-wizard mode only)
  const persistSelection = useCallback(
    (ids: Set<string>) => {
      if (wizardMode) {
        onSelectionChange?.(Array.from(ids));
        return;
      }
      if (debounceRef.current) clearTimeout(debounceRef.current);
      debounceRef.current = setTimeout(() => {
        mutation.mutate(Array.from(ids));
      }, 300);
    },
    [wizardMode, onSelectionChange, mutation]
  );

  // Radio handler for base documents (only 1 selected at a time)
  const handleBaseChange = useCallback(
    (docId: string) => {
      if (readOnly) return;
      setSelectedIds(prev => {
        const next = new Set(prev);
        // Remove any previously selected base document
        if (data) {
          for (const d of data.baseDocuments) next.delete(d.documentId);
        }
        next.add(docId);
        persistSelection(next);
        return next;
      });
    },
    [readOnly, data, persistSelection]
  );

  // Checkbox handler for additional documents
  const handleAdditionalChange = useCallback(
    (docId: string, selected: boolean) => {
      if (readOnly) return;
      setSelectedIds(prev => {
        const next = new Set(prev);
        if (selected) next.add(docId);
        else next.delete(docId);
        persistSelection(next);
        return next;
      });
    },
    [readOnly, persistSelection]
  );

  // Derived state
  const hasAgent = useMemo(() => data?.agentId != null, [data]);
  const isEmpty = useMemo(
    () => data != null && data.baseDocuments.length === 0 && data.additionalDocuments.length === 0,
    [data]
  );

  // ---- Render ----

  if (isLoading) return <LoadingSkeleton />;

  if (!wizardMode && !hasAgent && data) {
    return (
      <p className="rounded-md border border-[#30363d] bg-[#21262d] px-4 py-3 text-sm text-gray-400">
        Crea un agente prima di selezionare documenti
      </p>
    );
  }

  if (isEmpty) {
    return (
      <p className="rounded-md border border-[#30363d] bg-[#21262d] px-4 py-3 text-sm text-gray-400">
        Nessun documento disponibile
      </p>
    );
  }

  return (
    <div className="space-y-5">
      {/* Base documents (radio) */}
      {data && data.baseDocuments.length > 0 && (
        <section>
          <h4 className="mb-2 text-xs font-medium uppercase tracking-wider text-gray-400">
            Documento base
          </h4>
          <div className="space-y-2">
            {data.baseDocuments.map(doc => (
              <DocumentRow
                key={doc.documentId}
                doc={doc}
                inputType="radio"
                name={`base-doc-${gameId}`}
                checked={selectedIds.has(doc.documentId)}
                onChange={id => handleBaseChange(id)}
                readOnly={readOnly}
              />
            ))}
          </div>
        </section>
      )}

      {/* Additional documents (checkbox) */}
      {data && data.additionalDocuments.length > 0 && (
        <section>
          <h4 className="mb-2 text-xs font-medium uppercase tracking-wider text-gray-400">
            Documenti aggiuntivi
          </h4>
          <div className="space-y-2">
            {data.additionalDocuments.map(doc => (
              <DocumentRow
                key={doc.documentId}
                doc={doc}
                inputType="checkbox"
                checked={selectedIds.has(doc.documentId)}
                onChange={handleAdditionalChange}
                readOnly={readOnly}
              />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
