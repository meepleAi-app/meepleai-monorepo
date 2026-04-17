'use client';

/**
 * KbSelectionStep — KB document selection for session wizard
 *
 * Shown when a game has 2+ indexed KB documents. Lets the user
 * toggle which documents the AI agent should use during the session.
 *
 * Task 7: KB Selection Step in Session Wizard
 */

import { BookOpen } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
import { useKbGameDocuments } from '@/hooks/queries/useGameDocuments';
import { cn } from '@/lib/utils';

// ========== Props ==========

export interface KbSelectionStepProps {
  gameId: string;
  selectedDocIds: string[];
  onSelectionChange: (docIds: string[]) => void;
}

// ========== Category label map ==========

const CATEGORY_LABELS: Record<string, string> = {
  Rulebook: 'Regolamento',
  Expansion: 'Espansione',
  Errata: 'Errata',
  QuickStart: 'Avvio rapido',
  Reference: 'Riferimento',
  PlayerAid: 'Aiuto giocatore',
  Other: 'Altro',
};

// ========== Component ==========

export function KbSelectionStep({
  gameId,
  selectedDocIds,
  onSelectionChange,
}: KbSelectionStepProps) {
  const { data: documents = [], isLoading } = useKbGameDocuments(gameId);

  const toggle = (docId: string, checked: boolean) => {
    if (checked) {
      onSelectionChange([...selectedDocIds, docId]);
    } else {
      onSelectionChange(selectedDocIds.filter(id => id !== docId));
    }
  };

  return (
    <div className="space-y-3">
      <div>
        <h3 className="text-base font-semibold font-quicksand">Knowledge Base</h3>
        <p className="text-xs text-muted-foreground">
          Seleziona i documenti che l&apos;assistente AI potrà usare durante la partita.
        </p>
      </div>

      {isLoading ? (
        <p className="text-sm text-muted-foreground py-4 text-center">Caricamento documenti...</p>
      ) : documents.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4 text-center">
          Nessun documento disponibile.
        </p>
      ) : (
        <div className="space-y-2" role="list" aria-label="Documenti knowledge base">
          {documents.map(doc => {
            const isSelected = selectedDocIds.includes(doc.id);
            const categoryLabel = CATEGORY_LABELS[doc.category] ?? doc.category;
            const displayTitle = doc.title || doc.category;

            return (
              <div
                key={doc.id}
                role="listitem"
                className={cn(
                  'flex items-center gap-3 rounded-xl border p-3 transition-colors',
                  isSelected ? 'border-amber-500/40 bg-amber-500/5' : 'border-border bg-card'
                )}
              >
                {/* Icon */}
                <div
                  className={cn(
                    'h-8 w-8 rounded-lg flex items-center justify-center shrink-0',
                    isSelected
                      ? 'bg-amber-500/20 text-amber-500'
                      : 'bg-slate-200 dark:bg-slate-700 text-muted-foreground'
                  )}
                >
                  <BookOpen className="h-4 w-4" />
                </div>

                {/* Title + badge */}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium truncate">{displayTitle}</p>
                  <span className="inline-block text-[10px] uppercase tracking-wide font-semibold text-muted-foreground bg-slate-100 dark:bg-slate-800 rounded px-1.5 py-0.5 mt-0.5">
                    {categoryLabel}
                  </span>
                </div>

                {/* Toggle */}
                <Switch
                  checked={isSelected}
                  onCheckedChange={checked => toggle(doc.id, checked)}
                  aria-label={`Attiva documento ${displayTitle}`}
                />
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
