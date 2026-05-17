/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or decorative inline gradient; mockup .e-bg pattern. Will be re-evaluated in DS-15 finalization audit. */
'use client';

import { useState } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Live', 'Toolkit', 'Timeline'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function SessionDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Live'
  );

  return (
    <>
      <div className="flex border-b border-border px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(240,60%,65%)] border-b-2 border-[hsl(240,60%,55%)]'
                : 'text-white/45 hover:text-foreground/80'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Live' && <p>Stato live della sessione disponibile a breve.</p>}
        {tab === 'Toolkit' && <p>Tool attivi nella sessione disponibili a breve.</p>}
        {tab === 'Timeline' && <p>Timeline degli eventi disponibile a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-border flex-shrink-0">
        <Link
          href={`/sessions/${entityId}`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(240,60%,55%)] rounded-xl hover:bg-[hsl(240,60%,48%)] transition-colors"
        >
          ▶ Riprendi
        </Link>
        <Link
          href={`/sessions/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-foreground/80 border border-border rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
