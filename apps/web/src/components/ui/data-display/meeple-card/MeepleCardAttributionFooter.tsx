/**
 * Attribution footer for the WINNING cover source's license + author credit.
 *
 * Issue #2055 Phase G AC-G6 / epic #3470 Slice 3b (source-neutral: serves both
 * Wikidata and the admin-attested Manual cover — the value follows whichever
 * source won on the backend, never a fixed source).
 * DEC-G6-1 LOCKED: render as plain text — BE strips HTML upstream via
 * AttributionTextExtractor. Do NOT use dangerouslySetInnerHTML.
 *
 * <para>This footer is the canonical surface for the licensed cover credit on
 * the SharedGame catalog. It is rendered by MeepleCard when `entity === 'game'`
 * and the `coverLicense` prop is non-null. The component returns `null` when the
 * license is missing, so callers can wire it unconditionally without guards.</para>
 *
 * <para>Distinct from the older <c>CoverAttributionChip</c> (Issue #1823
 * Wave 3 M14) which targets the GridCard cover overlay. A future
 * follow-up may unify the two surfaces; for now they co-exist because
 * the M14 chip already serves L2 covers in a different layout position.</para>
 */

import type { JSX } from 'react';

type Props = {
  license: string | null;
  attribution: string | null;
  sourceUrl: string | null;
};

export function MeepleCardAttributionFooter({
  license,
  attribution,
  sourceUrl,
}: Props): JSX.Element | null {
  if (!license) {
    return null;
  }

  return (
    <footer className="mt-1 px-2 text-xs text-muted-foreground">
      <span>{license}</span>
      {attribution && (
        <>
          {' · '}
          <span>{attribution}</span>
        </>
      )}
      {sourceUrl && (
        <>
          {' · '}
          <a
            href={sourceUrl}
            rel="nofollow noopener noreferrer"
            target="_blank"
            className="underline"
          >
            source
          </a>
        </>
      )}
    </footer>
  );
}
