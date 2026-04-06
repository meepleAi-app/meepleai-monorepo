'use client';

import { LayoutGrid, Plus, Search, SortAsc } from 'lucide-react';
import { useRouter } from 'next/navigation';

export type FabPage = 'dashboard' | 'library' | 'sessions' | 'chat';

export interface FloatingActionPillProps {
  page: FabPage;
  onSearch?: () => void;
  onSort?: () => void;
  onToggleView?: () => void;
}

const PAGE_CONFIG: Record<FabPage, { label: string; cta: string; href: string }> = {
  dashboard: { label: '🏠 Dashboard', cta: '+ Aggiungi gioco', href: '/library?action=add' },
  library: { label: '📚 La mia libreria', cta: '+ Aggiungi', href: '/library?action=add' },
  sessions: { label: '🎯 Sessioni', cta: '▶ Nuova sessione', href: '/sessions/new' },
  chat: { label: '💬 Chat AI', cta: '+ Nuova chat', href: '/chat/new' },
};

/**
 * FloatingActionPill — FAB contestuale per le 4 pagine principali.
 *
 * Desktop (lg+): pillola centrata con backdrop-blur.
 * Mobile (<lg): FAB circolare bottom-20 right-4 + action bar opzionale.
 */
export function FloatingActionPill({
  page,
  onSearch,
  onSort,
  onToggleView,
}: FloatingActionPillProps) {
  const router = useRouter();
  const { label, cta, href } = PAGE_CONFIG[page];
  const hasSecondary = Boolean(onSearch ?? onSort ?? onToggleView);

  return (
    <>
      {/* ── Desktop: pillola centrata ── */}
      <div className="hidden lg:flex fixed bottom-6 left-1/2 -translate-x-1/2 z-50 items-center gap-2 px-4 py-2 bg-[rgba(30,41,59,0.85)] backdrop-blur-md border border-white/10 rounded-[40px] shadow-[0_8px_32px_rgba(0,0,0,0.4)]">
        <span className="text-xs font-quicksand font-bold text-muted-foreground pr-1">{label}</span>
        <div className="w-px h-5 bg-white/10" />
        <button
          type="button"
          onClick={() => router.push(href)}
          className="text-xs font-nunito font-bold text-white px-3 py-1.5 rounded-full hover:opacity-90 transition-opacity"
          style={{ background: 'hsl(var(--e-game))' }}
        >
          {cta}
        </button>
        {onSearch && (
          <button
            type="button"
            onClick={onSearch}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Cerca"
          >
            <Search className="w-3.5 h-3.5" />
          </button>
        )}
        {onSort && (
          <button
            type="button"
            onClick={onSort}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Ordina"
          >
            <SortAsc className="w-3.5 h-3.5" />
          </button>
        )}
        {onToggleView && (
          <button
            type="button"
            onClick={onToggleView}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Cambia vista"
          >
            <LayoutGrid className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {/* ── Mobile: action bar + FAB circolare ── */}
      <div className="lg:hidden">
        {hasSecondary && (
          <div className="fixed bottom-0 left-0 right-0 h-16 z-50 bg-[rgba(15,23,42,0.95)] backdrop-blur-md border-t border-white/[0.08] flex items-center justify-around px-6">
            {onSearch && (
              <button
                type="button"
                onClick={onSearch}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Cerca"
              >
                <Search className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Cerca</span>
              </button>
            )}
            {onSort && (
              <button
                type="button"
                onClick={onSort}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Ordina"
              >
                <SortAsc className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Ordina</span>
              </button>
            )}
            {onToggleView && (
              <button
                type="button"
                onClick={onToggleView}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Vista"
              >
                <LayoutGrid className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Vista</span>
              </button>
            )}
          </div>
        )}
        <button
          type="button"
          onClick={() => router.push(href)}
          className={`fixed ${hasSecondary ? 'bottom-20' : 'bottom-6'} right-4 z-50 w-14 h-14 rounded-full text-white flex items-center justify-center hover:opacity-90 transition-opacity`}
          style={{
            background: 'hsl(var(--e-game))',
            boxShadow: '0 4px 16px hsl(var(--e-game) / 0.4)',
          }}
          aria-label={cta}
        >
          <Plus className="w-6 h-6" />
        </button>
      </div>
    </>
  );
}
