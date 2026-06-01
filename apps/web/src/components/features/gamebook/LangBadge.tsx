'use client';

import type { ReactElement } from 'react';

import { LANG_LABELS_IT, type SourceLangCode } from '@/lib/gamebook/lang-codes';
import type { LangTier } from '@/lib/gamebook/lang-tier';

export interface LangBadgeProps {
  lang: SourceLangCode | null;
  confidence: number | null;
  tier: LangTier;
  onTap?: () => void;
  /** Render "(override)" suffix when user has manually overridden detected lang. */
  isOverride?: boolean;
}

const TIER_STYLES: Record<Exclude<LangTier, 'none'>, string> = {
  high: 'bg-emerald-100 text-emerald-900 ring-1 ring-emerald-200',
  medium: 'bg-amber-100 text-amber-900 ring-1 ring-amber-200',
  low: 'bg-rose-100 text-rose-900 ring-1 ring-rose-200',
};

const TIER_CONFIDENCE_LABEL_IT: Record<Exclude<LangTier, 'none'>, string> = {
  high: 'Confidenza alta',
  medium: 'Confidenza media',
  low: 'Confidenza bassa',
};

function buildVisualText(
  tier: Exclude<LangTier, 'none'>,
  lang: SourceLangCode | null,
  isOverride: boolean
): string {
  if (tier === 'low') return 'Sorgente richiesta';
  if (tier === 'medium' && lang) return `Conferma: ${lang}?`;
  if (tier === 'medium' && !lang) return 'Conferma sorgente';
  // tier === 'high'
  return isOverride ? `Sorgente: ${lang} (override)` : `Sorgente: ${lang}`;
}

function buildAriaLabel(
  tier: Exclude<LangTier, 'none'>,
  lang: SourceLangCode | null,
  confidence: number | null
): string {
  const langLabel = lang ? LANG_LABELS_IT[lang] : 'sconosciuta';
  const confLabel = TIER_CONFIDENCE_LABEL_IT[tier];
  const confPct =
    confidence !== null && confidence !== undefined ? ` (${Math.round(confidence * 100)}%)` : '';

  if (tier === 'low') {
    return `Sorgente richiesta. Conferma manualmente la lingua.`;
  }
  if (tier === 'medium') {
    return `Conferma sorgente: ${langLabel}. ${confLabel}${confPct}. Tap per confermare o cambiare.`;
  }
  return `Sorgente: ${langLabel}. ${confLabel}${confPct}. Tap per cambiare.`;
}

export function LangBadge({
  lang,
  confidence,
  tier,
  onTap,
  isOverride = false,
}: LangBadgeProps): ReactElement | null {
  if (tier === 'none') return null;

  const visualText = buildVisualText(tier, lang, isOverride);
  const baseClass = `inline-flex items-center rounded-full px-3 py-1 text-xs font-medium ${TIER_STYLES[tier]}`;

  if (onTap) {
    return (
      <button
        type="button"
        onClick={onTap}
        className={`${baseClass} cursor-pointer hover:opacity-90 focus-visible:ring-2 focus-visible:ring-offset-1`}
        aria-label={buildAriaLabel(tier, lang, confidence)}
        data-testid="lang-badge"
        data-tier={tier}
      >
        {visualText}
      </button>
    );
  }

  return (
    <span className={baseClass} data-testid="lang-badge" data-tier={tier}>
      {visualText}
    </span>
  );
}
