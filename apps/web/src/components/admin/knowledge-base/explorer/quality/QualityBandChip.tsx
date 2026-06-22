/**
 * QualityBandChip — Red/Yellow/Green band visual chip with IT label.
 * Renders an em-dash placeholder when band is null (run still in flight
 * or no metrics yet).
 */

import type { JSX } from 'react';

import type { QualityBand } from '@/lib/api/schemas/kb-quality.schemas';
import { formatQualityBand } from '@/lib/format/quality-band';

export interface QualityBandChipProps {
  readonly band: QualityBand | null;
}

export function QualityBandChip({ band }: QualityBandChipProps): JSX.Element {
  if (band === null) {
    return (
      <span
        data-testid="quality-band-chip-empty"
        className="inline-flex items-center rounded-full border border-border bg-muted px-2 py-0.5 text-[10px] font-semibold text-muted-foreground"
      >
        —
      </span>
    );
  }

  const style = formatQualityBand(band);
  return (
    <span
      data-testid={`quality-band-chip-${band.toLowerCase()}`}
      className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-semibold ${style.tailwindBg} ${style.tailwindText} ${style.tailwindBorder}`}
    >
      <span aria-hidden="true">{style.icon}</span>
      {style.label}
    </span>
  );
}
