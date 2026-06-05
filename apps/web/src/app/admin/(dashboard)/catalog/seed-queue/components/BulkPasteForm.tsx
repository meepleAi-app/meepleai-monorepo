'use client';

import { useMemo, useState } from 'react';

import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

import { BggIdValidationBadge, parseBggIds } from './BggIdValidationBadge';
import { useBulkEnqueueSeeds } from '../hooks/use-catalog-seeds';

/**
 * Issue #1903 M8.3 — bulk paste BGG IDs textarea + Enqueue button.
 *
 * Hard caps at 100 IDs per batch (BE enforces the same via
 * `BulkEnqueueCatalogSeedsCommandValidator`). When parsing produces invalid
 * tokens or duplicates within the same batch we surface them inline so the
 * admin can fix the input before submitting.
 */
const MAX_BATCH = 100;

export function BulkPasteForm() {
  const [text, setText] = useState('');
  const mutation = useBulkEnqueueSeeds();
  const parsed = useMemo(() => parseBggIds(text), [text]);
  const overCap = parsed.valid.length > MAX_BATCH;

  const handleSubmit = async () => {
    if (parsed.valid.length === 0 || overCap || mutation.isPending) return;
    try {
      const result = await mutation.mutateAsync({ bggIds: parsed.valid });
      toast.success(
        `Enqueued ${result.enqueued} draft${result.enqueued === 1 ? '' : 's'} (${result.duplicates} duplicate${result.duplicates === 1 ? '' : 's'} skipped)`
      );
      setText('');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Bulk enqueue failed');
    }
  };

  return (
    <section
      aria-label="Bulk paste BGG IDs"
      className="rounded-xl border border-border bg-card p-4"
    >
      <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">⊕ Add to queue</h3>
      <p className="mt-1 text-xs text-muted-foreground">
        Paste up to {MAX_BATCH} BGG IDs (one per line, comma- or space-separated).
      </p>
      <label className="mt-3 block">
        <span className="font-mono text-[10px] uppercase text-muted-foreground">BGG IDs</span>
        <textarea
          value={text}
          onChange={e => setText(e.target.value)}
          rows={6}
          className="mt-1 w-full rounded border border-border bg-background p-2 font-mono text-xs"
          placeholder="13\n30549\n167791"
        />
      </label>
      {parsed.valid.length + parsed.invalid.length + parsed.duplicates.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1.5">
          {parsed.valid.map(id => (
            <BggIdValidationBadge key={`v-${id}`} bggId={id} valid />
          ))}
          {parsed.invalid.map((item, idx) => (
            <BggIdValidationBadge
              key={`i-${idx}-${item.raw}`}
              bggId={item.raw}
              valid={false}
              reason={item.reason}
            />
          ))}
          {parsed.duplicates.map((id, idx) => (
            <BggIdValidationBadge
              key={`d-${idx}-${id}`}
              bggId={id}
              valid={false}
              reason="duplicate in batch"
            />
          ))}
        </div>
      )}
      {overCap && (
        <p className="mt-2 text-xs text-entity-event" role="alert">
          Batch cap exceeded — submit at most {MAX_BATCH} IDs per request.
        </p>
      )}
      <div className="mt-3 flex items-center gap-2">
        <Button
          onClick={handleSubmit}
          disabled={parsed.valid.length === 0 || overCap || mutation.isPending}
        >
          {mutation.isPending ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : null}
          Enqueue {parsed.valid.length} ID{parsed.valid.length === 1 ? '' : 's'}
        </Button>
        {parsed.valid.length > 0 && (
          <span className="font-mono text-[11px] text-muted-foreground">
            {parsed.valid.length}/{MAX_BATCH}
          </span>
        )}
      </div>
    </section>
  );
}
