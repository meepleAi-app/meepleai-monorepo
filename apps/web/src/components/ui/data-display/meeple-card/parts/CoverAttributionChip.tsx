'use client';

import type { CoverAttribution } from '../types';

/**
 * Issue #1823 Wave 3 M14 — cover image license + attribution surface for the
 * MeepleCard variants. Renders a compact `<small>` line with the author
 * credit + license tag (linked to the source URL when present).
 *
 * Visibility: returns `null` when no payload — variants can wire the
 * component unconditionally and the markup elides itself for L3 user-cover /
 * L4 PDF-cover that don't carry attribution. Only L2 Wikidata covers populate
 * the prop today (CC BY / CC BY-SA per ADR DEC-3c whitelist).
 */
export function CoverAttributionChip({ attribution }: { attribution?: CoverAttribution }) {
  if (!attribution) return null;

  const { author, license, sourceUrl } = attribution;
  if (!author && !license) return null;

  return (
    <small
      className="block text-[0.65rem] leading-tight text-[var(--mc-text-tertiary,var(--mc-text-secondary))]"
      data-slot="cover-attribution"
    >
      {author && <span className="font-medium">{author}</span>}
      {author && license && <span aria-hidden="true"> · </span>}
      {license &&
        (sourceUrl ? (
          <a
            href={sourceUrl}
            target="_blank"
            rel="noopener noreferrer license"
            className="underline hover:text-[var(--mc-text-primary)]"
          >
            {license}
          </a>
        ) : (
          <span>{license}</span>
        ))}
    </small>
  );
}
