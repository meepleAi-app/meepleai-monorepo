'use client';

import { Bot, Gamepad2, Heart, MoreHorizontal, Play, Share2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

interface FloatingActionPillProps {
  /** Context label shown in the pill (e.g. game name) */
  contextLabel: string;
  /** Primary CTA label (default: "Nuova sessione") */
  primaryLabel?: string;
  /** Callbacks */
  onPrimaryAction?: () => void;
  onAiChat?: () => void;
  onFavorite?: () => void;
  onShare?: () => void;
  onMore?: () => void;
  /** Whether the entity is already in favourites */
  isFavorited?: boolean;
}

/**
 * FloatingActionPill — Tier 3 of the 3-tier layout system.
 *
 * Desktop: glassmorphism pill fixed bottom-6 center.
 * Mobile: context breadcrumb strip (top-[92px]) + bottom action bar + FAB.
 *
 * NOT part of the global shell — import in entity detail pages.
 */
export function FloatingActionPill({
  contextLabel,
  primaryLabel = 'Nuova sessione',
  onPrimaryAction,
  onAiChat,
  onFavorite,
  onShare,
  onMore,
  isFavorited = false,
}: FloatingActionPillProps) {
  return (
    <>
      {/* ── Desktop floating pill ─────────────────────────────────── */}
      <div className="hidden sm:flex fixed bottom-6 left-1/2 -translate-x-1/2 z-[45]">
        <div
          className={cn(
            'flex items-center gap-2 px-3 py-2',
            'bg-[rgba(30,41,59,0.85)] backdrop-blur-md',
            'border border-white/10 rounded-[40px]',
            'shadow-[0_8px_32px_rgba(0,0,0,0.4)]'
          )}
        >
          {/* Context label */}
          <div className="flex items-center gap-2 px-2">
            <Gamepad2 className="w-4 h-4 text-entity-game" />
            <span className="text-muted-foreground font-body font-semibold text-[11px] max-w-[160px] truncate">
              {contextLabel}
            </span>
          </div>

          {/* Divider */}
          <div className="w-px h-6 bg-white/10" />

          {/* Actions */}
          <div className="flex items-center gap-1">
            <Button
              size="sm"
              onClick={onPrimaryAction}
              className={cn(
                'h-8 px-4 rounded-full',
                'bg-entity-game hover:bg-entity-game/90 text-white',
                'font-body font-bold text-xs'
              )}
            >
              <Play className="w-3.5 h-3.5 mr-1.5" />
              {primaryLabel}
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onAiChat}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
              title="Chat AI"
            >
              <Bot className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onFavorite}
              className={cn(
                'w-8 h-8 rounded-full hover:bg-white/5',
                isFavorited ? 'text-entity-event' : 'text-muted-foreground hover:text-foreground'
              )}
              title={isFavorited ? 'Rimuovi dai preferiti' : 'Aggiungi ai preferiti'}
            >
              <Heart className={cn('w-4 h-4', isFavorited && 'fill-entity-event')} />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onShare}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
              title="Condividi"
            >
              <Share2 className="w-4 h-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={onMore}
              className="w-8 h-8 rounded-full text-muted-foreground hover:text-foreground hover:bg-white/5"
              title="Altro"
            >
              <MoreHorizontal className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* ── Mobile: context breadcrumb + bottom bar + FAB ────────── */}
      <div className="sm:hidden">
        {/* Context breadcrumb strip (below TopNavbar + MiniNav) */}
        <div className="fixed top-[92px] left-0 right-0 z-40 h-10 bg-card/95 backdrop-blur-sm border-b border-border/50 px-4 flex items-center">
          <div className="flex items-center gap-2">
            <Gamepad2 className="w-4 h-4 text-entity-game" />
            <span className="text-foreground font-body font-semibold text-sm truncate">
              {contextLabel}
            </span>
          </div>
        </div>

        {/* Bottom action bar */}
        <div className="fixed bottom-0 left-0 right-0 z-[45] h-16 bg-card border-t border-border/50 px-4 pb-[env(safe-area-inset-bottom)]">
          <div className="h-full flex items-center justify-around">
            <button
              onClick={onAiChat}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <Bot className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">AI</span>
            </button>
            <button
              onClick={onFavorite}
              className={cn(
                'flex flex-col items-center gap-1 transition-colors',
                isFavorited ? 'text-entity-event' : 'text-muted-foreground hover:text-foreground'
              )}
            >
              <Heart className={cn('w-5 h-5', isFavorited && 'fill-entity-event')} />
              <span className="text-[10px] font-body font-semibold">Preferiti</span>
            </button>
            <button
              onClick={onShare}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <Share2 className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Condividi</span>
            </button>
            <button
              onClick={onMore}
              className="flex flex-col items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
            >
              <MoreHorizontal className="w-5 h-5" />
              <span className="text-[10px] font-body font-semibold">Altro</span>
            </button>
          </div>
        </div>

        {/* Orange FAB */}
        <div className="fixed bottom-20 right-4 z-[45]">
          <Button
            size="icon"
            onClick={onPrimaryAction}
            className={cn(
              'w-12 h-12 rounded-full',
              'bg-entity-game hover:bg-entity-game/90 text-white',
              'shadow-lg shadow-entity-game/30'
            )}
          >
            <Play className="w-5 h-5" />
          </Button>
        </div>
      </div>
    </>
  );
}
