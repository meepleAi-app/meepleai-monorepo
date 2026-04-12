'use client';

import { useState } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Profilo', 'Statistiche', 'Storico'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function PlayerDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Profilo'
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
                ? 'text-[hsl(262,83%,68%)] border-b-2 border-[hsl(262,83%,58%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Profilo' && <p>Profilo del giocatore disponibile a breve.</p>}
        {tab === 'Statistiche' && <p>Statistiche del giocatore disponibili a breve.</p>}
        {tab === 'Storico' && <p>Storico partite disponibile a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <button
          type="button"
          className="flex-1 py-2 text-center text-[11px] font-semibold text-white/70 border border-white/15 rounded-xl hover:text-white/90 transition-colors"
        >
          📊 Confronta
        </button>
        <Link
          href={`/players/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
