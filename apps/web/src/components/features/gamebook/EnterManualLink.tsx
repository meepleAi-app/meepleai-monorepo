'use client';

import type { ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { trackEvent } from '@/lib/analytics/track-event';

export type ManualEntryPoint = 'error_cta' | 'kebab' | 'empty_state';

export interface EnterManualLinkProps {
  entryPoint: ManualEntryPoint;
  campaignId: string;
}

export function EnterManualLink({ entryPoint, campaignId }: EnterManualLinkProps): ReactElement {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const handleClick = () => {
    trackEvent('translate.manual_entry_click', { entryPoint, campaignId });
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('mode', 'manual');
    router.push(`${pathname}?${params.toString()}`);
  };

  if (entryPoint === 'empty_state') {
    return (
      <button
        type="button"
        onClick={handleClick}
        className="m-4 flex items-center gap-3 rounded-lg border border-dashed border-[var(--c-agent)]/35 bg-[var(--c-agent)]/[0.06] p-4 text-left hover:bg-[var(--c-agent)]/[0.12] focus-visible:ring-2 focus-visible:ring-[var(--c-agent)]"
        data-testid="enter-manual-empty-state"
      >
        <span className="text-2xl" aria-hidden>
          📝
        </span>
        <span className="flex-1">
          <span className="block font-bold">Libro non a portata?</span>
          <span className="block text-xs text-muted-foreground">
            Digita il paragrafo manualmente
          </span>
        </span>
        <span aria-hidden className="text-lg font-bold text-[var(--c-agent)]">
          →
        </span>
      </button>
    );
  }

  if (entryPoint === 'kebab') {
    return (
      <button
        type="button"
        role="menuitem"
        onClick={handleClick}
        className="w-full px-4 py-2 text-left text-sm hover:bg-muted focus-visible:bg-muted"
        data-testid="enter-manual-kebab"
      >
        Digita manualmente
      </button>
    );
  }

  // error_cta variant
  return (
    <button
      type="button"
      onClick={handleClick}
      className="text-sm text-[var(--c-agent)] underline hover:no-underline focus-visible:ring-2"
      data-testid="enter-manual-error-cta"
    >
      Digita manualmente →
    </button>
  );
}
