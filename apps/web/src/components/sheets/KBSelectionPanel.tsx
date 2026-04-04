/**
 * KBSelectionPanel — KB document selection panel for agent creation wizard.
 *
 * Fetches documents for a shared game, auto-selects the latest Rulebook + Errata,
 * and lets the user pick optional expansions / homerules before confirming.
 */

'use client';

import { useState, useMemo, useEffect, useCallback } from 'react';

import { ArrowLeft, BookOpen, FileText, Check, Loader2, AlertCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { useSharedGameDocuments } from '@/hooks/queries/useSharedGameDocuments';
import type { SharedGameDocument } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface KBSelectionPanelProps {
  gameId: string;
  gameTitle: string;
  onBack: () => void;
  onConfirm: (selectedDocumentIds: string[], summary: string) => void;
}

// ─── Constants ───────────────────────────────────────────────────────────────

const DOCUMENT_TYPE_CONFIG: Record<number, { label: string; badgeClass: string; emoji: string }> = {
  0: {
    label: 'Rulebook',
    badgeClass: 'bg-blue-100 text-blue-800 border-blue-300',
    emoji: '📖',
  },
  1: {
    label: 'Errata',
    badgeClass: 'bg-orange-100 text-orange-800 border-orange-300',
    emoji: '📝',
  },
  2: {
    label: 'Homerule',
    badgeClass: 'bg-green-100 text-green-800 border-green-300',
    emoji: '🏠',
  },
};

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Parse a version string like "1.2" into a comparable numeric value.
 * Higher = newer.
 */
function parseVersion(version: string): number {
  const [major = 0, minor = 0] = version.split('.').map(Number);
  return major * 1000 + minor;
}

/**
 * From a list of documents of the same type, return the one with the highest version.
 */
function getLatest(docs: SharedGameDocument[]): SharedGameDocument | undefined {
  if (docs.length === 0) return undefined;
  return docs.reduce((best, d) =>
    parseVersion(d.version) > parseVersion(best.version) ? d : best
  );
}

/**
 * Build the auto-selection: latest Rulebook + latest Errata (if any).
 */
function buildAutoSelection(docs: SharedGameDocument[]): Set<string> {
  const byType = docs.reduce(
    (acc, d) => {
      if (!acc[d.documentType]) acc[d.documentType] = [];
      acc[d.documentType].push(d);
      return acc;
    },
    {} as Record<number, SharedGameDocument[]>
  );

  const selected = new Set<string>();

  const latestRulebook = getLatest(byType[0] ?? []);
  if (latestRulebook) selected.add(latestRulebook.id);

  const latestErrata = getLatest(byType[1] ?? []);
  if (latestErrata) selected.add(latestErrata.id);

  return selected;
}

/**
 * Build a human-readable summary string for the selected documents.
 */
function buildSummary(docs: SharedGameDocument[], selectedIds: Set<string>): string {
  const selected = docs.filter(d => selectedIds.has(d.id));
  const parts: string[] = [];

  const rulebookCount = selected.filter(d => d.documentType === 0).length;
  const errataCount = selected.filter(d => d.documentType === 1).length;
  const homeruleCount = selected.filter(d => d.documentType === 2).length;

  if (rulebookCount > 0) parts.push(`${rulebookCount} rulebook${rulebookCount > 1 ? 's' : ''}`);
  if (errataCount > 0) parts.push(`${errataCount} errata`);
  if (homeruleCount > 0) parts.push(`${homeruleCount} homerule${homeruleCount > 1 ? 's' : ''}`);

  return parts.join(', ');
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function DocumentRow({
  doc,
  isSelected,
  isAutoSelected,
  isDisabled,
  onToggle,
}: {
  doc: SharedGameDocument;
  isSelected: boolean;
  isAutoSelected: boolean;
  isDisabled: boolean;
  onToggle: (id: string) => void;
}) {
  const typeConfig = DOCUMENT_TYPE_CONFIG[doc.documentType];

  return (
    <div
      role="checkbox"
      aria-checked={isSelected}
      tabIndex={isDisabled ? -1 : 0}
      className={cn(
        'flex items-center gap-3 p-3 rounded-lg border transition-colors cursor-pointer',
        isSelected
          ? isAutoSelected
            ? 'bg-green-50 border-green-400 dark:bg-green-900/20 dark:border-green-600'
            : 'bg-primary/10 border-primary'
          : 'hover:bg-accent/50 border-transparent',
        isDisabled && 'opacity-50 cursor-not-allowed'
      )}
      onClick={() => !isDisabled && onToggle(doc.id)}
      onKeyDown={e => {
        if ((e.key === 'Enter' || e.key === ' ') && !isDisabled) {
          e.preventDefault();
          onToggle(doc.id);
        }
      }}
    >
      <Checkbox
        checked={isSelected}
        disabled={isDisabled}
        className="pointer-events-none shrink-0"
      />

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          {typeConfig && (
            <Badge variant="outline" className={cn('text-xs', typeConfig.badgeClass)}>
              {typeConfig.emoji} {typeConfig.label}
            </Badge>
          )}
          <Badge variant="secondary" className="text-xs">
            v{doc.version}
          </Badge>
          {doc.isActive && (
            <Badge variant="default" className="text-xs">
              Attivo
            </Badge>
          )}
          {doc.tags?.map(tag => (
            <Badge key={tag} variant="outline" className="text-xs">
              {tag}
            </Badge>
          ))}
        </div>
        <p className="text-xs text-muted-foreground mt-1">
          Aggiunto: {new Date(doc.createdAt).toLocaleDateString('it-IT')}
        </p>
      </div>

      {isSelected && isAutoSelected && <Check className="h-4 w-4 text-green-600 shrink-0" />}
      {isSelected && !isAutoSelected && <Check className="h-4 w-4 text-primary shrink-0" />}
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function KBSelectionPanel({ gameId, gameTitle, onBack, onConfirm }: KBSelectionPanelProps) {
  const { data: documents = [], isLoading, error } = useSharedGameDocuments(gameId);

  // Set of IDs the user has selected
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Tracks which IDs were auto-selected (for styling)
  const autoSelected = useMemo(() => buildAutoSelection(documents), [documents]);

  // Initialise selection once documents load
  useEffect(() => {
    if (documents.length > 0) {
      setSelectedIds(buildAutoSelection(documents));
    }
  }, [documents]);

  // Group documents for display
  const grouped = useMemo(() => {
    return documents.reduce(
      (acc, doc) => {
        if (!acc[doc.documentType]) acc[doc.documentType] = [];
        acc[doc.documentType].push(doc);
        return acc;
      },
      {} as Record<number, SharedGameDocument[]>
    );
  }, [documents]);

  const handleToggle = useCallback(
    (id: string) => {
      const currentRulebookDocs = grouped[0] ?? [];
      setSelectedIds(prev => {
        const next = new Set(prev);
        const doc = documents.find(d => d.id === id);

        if (!doc) return prev;

        if (next.has(id)) {
          // Prevent deselecting the only selected Rulebook
          if (doc.documentType === 0) {
            const selectedRuleBooks = [...next].filter(
              selId => documents.find(d => d.id === selId)?.documentType === 0
            );
            if (selectedRuleBooks.length <= 1) return prev;
          }
          next.delete(id);
        } else {
          // Only 1 Rulebook version at a time: deselect any other Rulebook
          if (doc.documentType === 0) {
            currentRulebookDocs.forEach(rb => next.delete(rb.id));
          }
          next.add(id);
        }

        return next;
      });
    },
    [documents, grouped]
  );

  const handleConfirm = useCallback(() => {
    const ids = [...selectedIds];
    const summary = buildSummary(documents, selectedIds);
    onConfirm(ids, summary);
  }, [selectedIds, documents, onConfirm]);

  const canConfirm = selectedIds.size > 0;
  const selectedCount = selectedIds.size;

  // ─── Loading / Error states ─────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-3">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        <p className="text-sm text-muted-foreground">Caricamento documenti...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-3 px-4 text-center">
        <AlertCircle className="h-8 w-8 text-red-500" />
        <p className="text-sm text-red-700 dark:text-red-300">
          Impossibile caricare i documenti. Riprova.
        </p>
        <Button variant="outline" size="sm" onClick={onBack}>
          Torna indietro
        </Button>
      </div>
    );
  }

  if (documents.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-3 px-4 text-center">
        <BookOpen className="h-10 w-10 text-muted-foreground/40" />
        <p className="text-sm text-muted-foreground">
          Nessun documento disponibile per <strong>{gameTitle}</strong>.
        </p>
        <Button variant="outline" size="sm" onClick={onBack}>
          Torna indietro
        </Button>
      </div>
    );
  }

  // ─── Render ─────────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col h-full">
      {/* Sub-header */}
      <div className="px-4 py-3 border-b flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          className="h-7 w-7 shrink-0"
          onClick={onBack}
          aria-label="Torna alla ricerca gioco"
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div className="min-w-0">
          <p className="text-sm font-medium truncate">{gameTitle}</p>
          <p className="text-xs text-muted-foreground">Seleziona Knowledge Base</p>
        </div>
      </div>

      {/* Auto-selection notice */}
      <div className="mx-4 mt-3 p-2.5 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
        <p className="text-xs text-green-700 dark:text-green-300">
          Pre-selezionato: Rulebook{documents.some(d => d.documentType === 1) ? ' e Errata' : ''}{' '}
          pi&ugrave; recenti. Puoi modificare la selezione.
        </p>
      </div>

      {/* Document list */}
      <ScrollArea className="flex-1 mt-3 px-4">
        <div className="space-y-5 pb-4">
          {Object.entries(grouped)
            .sort(([a], [b]) => parseInt(a) - parseInt(b))
            .map(([type, docs]) => {
              const typeNum = parseInt(type);
              const typeConfig = DOCUMENT_TYPE_CONFIG[typeNum];

              // Sort docs within group: newest version first
              const sorted = [...docs].sort(
                (a, b) => parseVersion(b.version) - parseVersion(a.version)
              );

              return (
                <div key={type} className="space-y-2">
                  <div className="flex items-center gap-2">
                    <span className="text-sm">{typeConfig?.emoji}</span>
                    <span className="text-sm font-medium">
                      {typeConfig?.label ?? `Tipo ${type}`}
                    </span>
                    <span className="text-xs text-muted-foreground">({docs.length})</span>
                  </div>

                  <div className="space-y-1.5">
                    {sorted.map(doc => {
                      const isSelected = selectedIds.has(doc.id);
                      const isAutoSel = autoSelected.has(doc.id);

                      // Rulebook: can only deselect if there's >1 selected Rulebook
                      const selectedRulebookCount = [...selectedIds].filter(
                        selId => documents.find(d => d.id === selId)?.documentType === 0
                      ).length;
                      const isDisabled = typeNum === 0 && isSelected && selectedRulebookCount <= 1;

                      return (
                        <DocumentRow
                          key={doc.id}
                          doc={doc}
                          isSelected={isSelected}
                          isAutoSelected={isAutoSel && isSelected}
                          isDisabled={isDisabled}
                          onToggle={handleToggle}
                        />
                      );
                    })}
                  </div>
                </div>
              );
            })}
        </div>
      </ScrollArea>

      {/* Footer */}
      <div className="border-t px-4 py-3 mt-auto">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground min-w-0">
            <FileText className="h-3.5 w-3.5 shrink-0" />
            <span className="truncate">
              {buildSummary(documents, selectedIds) || 'Nessun documento selezionato'}
            </span>
          </div>
          <Button onClick={handleConfirm} disabled={!canConfirm} className="shrink-0" size="sm">
            Crea Agente con {selectedCount} KB →
          </Button>
        </div>
      </div>
    </div>
  );
}
