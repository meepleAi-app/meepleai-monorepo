'use client';

import { useState } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Overview', 'Preview', 'Citazioni'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function KbDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Overview'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(210,40%,65%)] border-b-2 border-[hsl(210,40%,55%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Overview' && <p>Dettagli documento disponibili a breve.</p>}
        {tab === 'Preview' && <p>Preview del testo estratto disponibile a breve.</p>}
        {tab === 'Citazioni' && <p>Citazioni generate disponibili a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <button
          type="button"
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-semibold text-white/70 border border-white/15 rounded-xl hover:text-white/90 transition-colors"
        >
          🔄 Reindex
        </button>
        <button
          type="button"
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ⬇ Download
        </button>
        <Link
          href={`/kb/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
