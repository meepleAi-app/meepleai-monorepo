'use client';

import { useState } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Info', 'Statistiche', 'Storico'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function GameDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Info'
  );

  return (
    <>
      {/* Tabs */}
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
                ? 'text-[hsl(25,95%,55%)] border-b-2 border-[hsl(25,95%,45%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Info' && <p>Dettagli del gioco disponibili dopo il caricamento dei dati.</p>}
        {tab === 'Statistiche' && <p>Statistiche di gioco disponibili a breve.</p>}
        {tab === 'Storico' && <p>Storico delle sessioni disponibile a breve.</p>}
      </div>

      {/* Footer */}
      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <Link
          href={`/games/${entityId}/sessions/new`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(25,95%,45%)] rounded-xl hover:bg-[hsl(25,95%,40%)] transition-colors"
        >
          ▶ Gioca
        </Link>
        <Link
          href={`/games/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
