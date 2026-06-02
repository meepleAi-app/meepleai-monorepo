/**
 * KB Quality band → display style mapping (#1675).
 *
 * Centralises the Red/Yellow/Green visual treatment so chips, badges, and
 * future sparkline overlays stay in sync. IT-localised labels because the
 * admin surface is Italian-first.
 */

import type { QualityBand } from '@/lib/api/schemas/kb-quality.schemas';

export interface QualityBandStyle {
  label: string;
  icon: string;
  tailwindBg: string;
  tailwindText: string;
  tailwindBorder: string;
}

export function formatQualityBand(band: QualityBand): QualityBandStyle {
  switch (band) {
    case 'Green':
      return {
        label: 'Verde',
        icon: '🟢',
        tailwindBg: 'bg-emerald-500/10',
        tailwindText: 'text-emerald-700 dark:text-emerald-300',
        tailwindBorder: 'border-emerald-500/30',
      };
    case 'Yellow':
      return {
        label: 'Giallo',
        icon: '🟡',
        tailwindBg: 'bg-amber-500/10',
        tailwindText: 'text-amber-700 dark:text-amber-300',
        tailwindBorder: 'border-amber-500/30',
      };
    case 'Red':
      return {
        label: 'Rosso',
        icon: '🔴',
        tailwindBg: 'bg-rose-500/10',
        tailwindText: 'text-rose-700 dark:text-rose-300',
        tailwindBorder: 'border-rose-500/30',
      };
  }
}
