import { Check, X } from 'lucide-react';

/**
 * Issue #1903 M8.4 — visual badge displayed next to parsed BGG IDs in the bulk
 * paste textarea preview. Indicates whether the line was accepted as a positive
 * integer ("valid") or rejected (e.g. non-numeric, duplicate within the same
 * batch).
 */
export interface BggIdValidationBadgeProps {
  bggId: number | string;
  valid: boolean;
  reason?: string;
}

export function BggIdValidationBadge({ bggId, valid, reason }: BggIdValidationBadgeProps) {
  if (valid) {
    return (
      <span
        className="inline-flex items-center gap-1 rounded-full bg-entity-collection/[0.12] px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.04em] text-entity-collection"
        data-testid={`bgg-id-badge-${bggId}`}
        data-state="valid"
      >
        <Check className="h-3 w-3" aria-hidden />
        {bggId}
      </span>
    );
  }
  return (
    <span
      className="inline-flex items-center gap-1 rounded-full bg-entity-event/[0.12] px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.04em] text-entity-event"
      title={reason}
      data-testid={`bgg-id-badge-${bggId}`}
      data-state="invalid"
    >
      <X className="h-3 w-3" aria-hidden />
      {bggId}
      {reason ? <span className="ml-1 normal-case">— {reason}</span> : null}
    </span>
  );
}

/**
 * Parses a multi-line paste of BGG ids into `{valid, invalid}` buckets.
 * Recognises one id per line; rejects empty rows, non-numeric tokens, and
 * duplicates within the same batch.
 */
export interface ParsedBggIds {
  valid: number[];
  invalid: { raw: string; reason: string }[];
  duplicates: number[];
}

export function parseBggIds(input: string): ParsedBggIds {
  const seen = new Set<number>();
  const valid: number[] = [];
  const invalid: { raw: string; reason: string }[] = [];
  const duplicates: number[] = [];
  input
    .split(/[\n,;\s]+/)
    .map(line => line.trim())
    .filter(line => line.length > 0)
    .forEach(raw => {
      const n = Number.parseInt(raw, 10);
      if (!Number.isFinite(n) || String(n) !== raw || n <= 0) {
        invalid.push({ raw, reason: 'not a positive integer' });
        return;
      }
      if (seen.has(n)) {
        duplicates.push(n);
        return;
      }
      seen.add(n);
      valid.push(n);
    });
  return { valid, invalid, duplicates };
}
