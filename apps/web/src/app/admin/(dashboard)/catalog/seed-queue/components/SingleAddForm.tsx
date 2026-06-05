'use client';

import { useState } from 'react';

import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

import { useEnqueueSeed } from '../hooks/use-catalog-seeds';

/**
 * Issue #1903 M8.3 — single-row add form (BGG id, Wikidata QID, or free-text search).
 *
 * Replaces `AssignBggIdForm` from #1835 — instead of pairing a draft with an
 * existing shared-game UUID, this submits one of three possible inputs to the
 * BE which then drives the Wikidata / BGG provider chain.
 */
type Mode = 'bgg' | 'wikidata' | 'search';

export function SingleAddForm() {
  const [mode, setMode] = useState<Mode>('bgg');
  const [bggIdRaw, setBggIdRaw] = useState('');
  const [wikidataQid, setWikidataQid] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const mutation = useEnqueueSeed();

  const bggIdValid = /^\d+$/.test(bggIdRaw) && Number.parseInt(bggIdRaw, 10) > 0;
  const wikidataValid = /^Q\d+$/.test(wikidataQid);
  const searchValid = searchTerm.trim().length >= 2;

  const isValid =
    (mode === 'bgg' && bggIdValid) ||
    (mode === 'wikidata' && wikidataValid) ||
    (mode === 'search' && searchValid);

  const handleSubmit = async () => {
    if (!isValid || mutation.isPending) return;
    try {
      const body =
        mode === 'bgg'
          ? { bggId: Number.parseInt(bggIdRaw, 10) }
          : mode === 'wikidata'
            ? { wikidataQid }
            : { searchTermInput: searchTerm.trim() };
      const result = await mutation.mutateAsync(body);
      if (result.isDuplicate) {
        toast.info(
          `Draft already exists for ${mode === 'bgg' ? `BGG:${bggIdRaw}` : mode === 'wikidata' ? wikidataQid : searchTerm}`
        );
      } else {
        toast.success(`Seed enqueued: ${result.id}`);
      }
      setBggIdRaw('');
      setWikidataQid('');
      setSearchTerm('');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Enqueue failed');
    }
  };

  return (
    <section aria-label="Single seed add" className="rounded-xl border border-border bg-card p-4">
      <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">+ Single add</h3>
      <div className="mt-3 flex gap-2 font-mono text-[10px] uppercase">
        {(['bgg', 'wikidata', 'search'] as const).map(m => (
          <button
            key={m}
            type="button"
            onClick={() => setMode(m)}
            data-state={mode === m ? 'active' : 'inactive'}
            className="rounded border border-border bg-background px-2 py-1 data-[state=active]:bg-entity-toolkit/[0.12] data-[state=active]:text-entity-toolkit"
          >
            {m === 'bgg' ? 'BGG ID' : m === 'wikidata' ? 'Wikidata QID' : 'Search term'}
          </button>
        ))}
      </div>
      {mode === 'bgg' && (
        <label className="mt-3 block">
          <span className="font-mono text-[10px] uppercase text-muted-foreground">BGG ID</span>
          <input
            aria-label="BGG ID"
            inputMode="numeric"
            value={bggIdRaw}
            onChange={e => setBggIdRaw(e.target.value)}
            className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 font-mono text-sm"
            placeholder="e.g. 13"
          />
        </label>
      )}
      {mode === 'wikidata' && (
        <label className="mt-3 block">
          <span className="font-mono text-[10px] uppercase text-muted-foreground">
            Wikidata QID
          </span>
          <input
            aria-label="Wikidata QID"
            value={wikidataQid}
            onChange={e => setWikidataQid(e.target.value.toUpperCase())}
            className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 font-mono text-sm"
            placeholder="e.g. Q170416"
          />
        </label>
      )}
      {mode === 'search' && (
        <label className="mt-3 block">
          <span className="font-mono text-[10px] uppercase text-muted-foreground">Search term</span>
          <input
            aria-label="Search term"
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            className="mt-1 w-full rounded border border-border bg-background px-2 py-1.5 text-sm"
            placeholder="e.g. catan"
          />
        </label>
      )}
      <Button onClick={handleSubmit} disabled={!isValid || mutation.isPending} className="mt-3">
        {mutation.isPending ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : null}
        Enqueue draft
      </Button>
    </section>
  );
}
