'use client';

import { useMemo, useState } from 'react';

import { MessageCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import { isRulebook } from '@/lib/api/schemas/game-documents.schemas';

interface KbSelectorProps {
  documents: GameDocument[];
  onConfirm: (selectedIds: string[]) => void;
  onCancel?: () => void;
}

export function KbSelector({ documents, onConfirm, onCancel }: KbSelectorProps) {
  const indexedDocs = useMemo(() => documents.filter(d => d.status === 'indexed'), [documents]);
  const rulebooks = useMemo(() => indexedDocs.filter(isRulebook), [indexedDocs]);
  const otherDocs = useMemo(() => indexedDocs.filter(d => !isRulebook(d)), [indexedDocs]);

  // Default: latest rulebook by createdAt
  const latestRulebook = useMemo(
    () =>
      rulebooks.length > 0
        ? [...rulebooks].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0]
        : null,
    [rulebooks]
  );

  const [selectedRulebookId, setSelectedRulebookId] = useState<string | null>(
    latestRulebook?.id ?? null
  );
  // Default: all other docs selected
  const [selectedOtherIds, setSelectedOtherIds] = useState<Set<string>>(
    new Set(otherDocs.map(d => d.id))
  );

  const selectedIds = useMemo(() => {
    const ids: string[] = [];
    if (selectedRulebookId) ids.push(selectedRulebookId);
    selectedOtherIds.forEach(id => ids.push(id));
    return ids;
  }, [selectedRulebookId, selectedOtherIds]);

  const toggleOther = (docId: string) => {
    setSelectedOtherIds(prev => {
      const next = new Set(prev);
      if (next.has(docId)) next.delete(docId);
      else next.add(docId);
      return next;
    });
  };

  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-base font-semibold">Seleziona Knowledge Base</h2>

      {rulebooks.length > 0 && (
        <fieldset>
          <legend className="mb-2 text-sm font-medium text-muted-foreground">
            Regolamento (scegli versione)
          </legend>
          <div className="space-y-2">
            {rulebooks.map(doc => (
              <label
                key={doc.id}
                className="flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent/50"
              >
                <input
                  type="radio"
                  name="rulebook"
                  value={doc.id}
                  checked={selectedRulebookId === doc.id}
                  onChange={() => setSelectedRulebookId(doc.id)}
                  aria-label={doc.title}
                  className="h-4 w-4"
                />
                <div className="flex-1">
                  <span className="text-sm font-medium">{doc.title}</span>
                  {doc.versionLabel && (
                    <span className="ml-2 text-xs text-muted-foreground">({doc.versionLabel})</span>
                  )}
                </div>
              </label>
            ))}
          </div>
        </fieldset>
      )}

      {otherDocs.length > 0 && (
        <fieldset>
          <legend className="mb-2 text-sm font-medium text-muted-foreground">
            Altre Knowledge Base
          </legend>
          <div className="space-y-2">
            {otherDocs.map(doc => (
              <label
                key={doc.id}
                className="flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent/50"
              >
                <input
                  type="checkbox"
                  checked={selectedOtherIds.has(doc.id)}
                  onChange={() => toggleOther(doc.id)}
                  aria-label={doc.title}
                  className="h-4 w-4"
                />
                <div className="flex-1">
                  <span className="text-sm font-medium">{doc.title}</span>
                  {doc.versionLabel && (
                    <span className="ml-2 text-xs text-muted-foreground">({doc.versionLabel})</span>
                  )}
                </div>
              </label>
            ))}
          </div>
        </fieldset>
      )}

      <div className="flex gap-2 pt-2">
        {onCancel && (
          <Button variant="outline" className="flex-1" onClick={onCancel}>
            Annulla
          </Button>
        )}
        <Button
          className="flex-1"
          disabled={selectedIds.length === 0}
          onClick={() => onConfirm(selectedIds)}
        >
          <MessageCircle className="mr-2 h-4 w-4" />
          Avvia Chat
        </Button>
      </div>
    </div>
  );
}
