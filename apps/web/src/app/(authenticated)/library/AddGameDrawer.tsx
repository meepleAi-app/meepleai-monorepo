'use client';

/**
 * AddGameDrawer — Right-side Sheet for adding a game to the personal library
 *
 * Simplified flow (Fase 2 add-flow refactor):
 *   Step 0: Choose method — "Manually" or "From Catalog"
 *   Step 1a (Manual):  Embed UserWizardClient in compactMode (1-step game creation,
 *                      PDF/Agent setup deferred to detail-page CTAs)
 *   Step 1b (Catalog): CatalogSearchStep → select game → addGame to library →
 *                      close drawer + redirect /library/{gameId}
 *                      PDF/Agent setup happens on the detail page, not blocking the add.
 *
 * URL integration:
 *   ?action=add         → drawer opens (choice step)
 *   close / ESC         → removes ?action from URL
 *
 * Note: BGG search was removed from user pages (restricted to admin only due to licensing).
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import { CatalogSearchStep } from '@/app/(authenticated)/library/CatalogSearchStep';
import { UserWizardClient } from '@/app/(authenticated)/library/private/add/client';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

type DrawerStep = 'choice' | 'manual' | 'catalog';

// ─── Step 0: Choice cards ─────────────────────────────────────────────────────

/**
 * Visual accent per choice card. `game` for the manual (create-from-scratch)
 * path, `kb` for the catalog (knowledge-base / community catalog) path.
 *
 * Maps to the canonical entity tokens `--c-game` / `--c-kb` (see
 * `apps/web/src/styles/design-tokens-canonical.css`). Implements the SP4
 * mockup `sp4-add-game-drawer.jsx:99-145` per issue #2076.
 */
type ChoiceAccent = 'game' | 'kb';

interface ChoiceCardProps {
  accent: ChoiceAccent;
  /** Emoji glyph (decorative) — e.g. `✍️` for manual, `📚` for catalog. */
  glyph: string;
  title: string;
  description: string;
  onClick: () => void;
  'data-testid'?: string;
}

const ACCENT_STYLES: Record<
  ChoiceAccent,
  {
    border: string;
    glyphBg: string;
    chevron: string;
    ring: string;
    hoverBg: string;
  }
> = {
  game: {
    border: 'hover:border-[hsl(var(--c-game)/0.55)]',
    glyphBg: 'bg-[hsl(var(--c-game)/0.14)]',
    chevron: 'group-hover:text-[hsl(var(--c-game))]',
    ring: 'group-hover:ring-2 group-hover:ring-[hsl(var(--c-game)/0.12)]',
    hoverBg: 'hover:bg-[hsl(var(--c-game)/0.05)]',
  },
  kb: {
    border: 'hover:border-[hsl(var(--c-kb)/0.55)]',
    glyphBg: 'bg-[hsl(var(--c-kb)/0.14)]',
    chevron: 'group-hover:text-[hsl(var(--c-kb))]',
    ring: 'group-hover:ring-2 group-hover:ring-[hsl(var(--c-kb)/0.12)]',
    hoverBg: 'hover:bg-[hsl(var(--c-kb)/0.05)]',
  },
};

function ChoiceCard({
  accent,
  glyph,
  title,
  description,
  onClick,
  'data-testid': testId,
}: ChoiceCardProps) {
  const styles = ACCENT_STYLES[accent];
  return (
    <button
      type="button"
      data-testid={testId}
      onClick={onClick}
      className={cn(
        'group w-full text-left rounded-xl border-[1.5px] border-border bg-card p-5 shadow-xs',
        'flex items-start gap-4',
        'hover:-translate-y-0.5 hover:shadow-sm',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        'transition-[transform,box-shadow,border-color,background-color] duration-150 ease-out cursor-pointer',
        styles.border,
        styles.hoverBg
      )}
    >
      {/*
        F2.2 T6 #1974 + #2076: glyph is purely decorative — title + description
        below carry the meaning. `aria-hidden` prevents screen readers from
        announcing the emoji before the labelled choice.
      */}
      <span
        aria-hidden="true"
        className={cn(
          'shrink-0 flex h-[46px] w-[46px] items-center justify-center rounded-lg text-[22px]',
          'transition-shadow duration-150 ease-out',
          styles.glyphBg,
          styles.ring
        )}
      >
        {glyph}
      </span>
      <span className="flex-1 min-w-0">
        <span className="block font-quicksand font-extrabold text-lg leading-tight text-foreground">
          {title}
        </span>
        <span className="block mt-1.5 text-base leading-snug text-muted-foreground">
          {description}
        </span>
      </span>
      <span
        aria-hidden="true"
        className={cn(
          'shrink-0 self-center text-lg font-extrabold text-muted-foreground',
          'transition-[color,transform] duration-150 ease-out group-hover:translate-x-0.5',
          styles.chevron
        )}
      >
        ›
      </span>
    </button>
  );
}

// ─── Main drawer ──────────────────────────────────────────────────────────────

interface AddGameDrawerProps {
  open: boolean;
  onClose: () => void;
}

export function AddGameDrawer({ open, onClose }: AddGameDrawerProps) {
  const router = useRouter();
  const { t } = useTranslation();
  const [step, setStep] = useState<DrawerStep>('choice');
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    };
  }, []);

  // #2012 — Telemetry: drawer-open event drives the conversion-funnel
  // denominator (open → choice rate, manual:catalog ratio, abandonment).
  useEffect(() => {
    if (open) {
      trackEvent('library_addgame_drawer_opened');
    }
  }, [open]);

  // Reset to choice step after close animation finishes
  const handleOpenChange = useCallback(
    (isOpen: boolean) => {
      if (!isOpen) {
        // #2012 — Telemetry: closing while still on the choice step counts
        // as abandonment (user opened the drawer but selected neither option).
        if (step === 'choice') {
          trackEvent('library_addgame_drawer_closed_without_choice');
        }
        onClose();
        closeTimerRef.current = setTimeout(() => {
          setStep('choice');
        }, 300);
      }
    },
    [onClose, step]
  );

  // Called by CatalogSearchStep after game is successfully added to library.
  // Simplified flow: no longer transitions to a PDF step — close drawer and
  // jump straight to the game detail page where PDF/Agent CTAs await.
  // #2269 P0-2 (M2) — shared with the blocked-alert CTA (`onNavigateToGame`)
  // so the "Vai alla scheda" path produces the same close+redirect as a
  // successful add.
  const handleNavigateToGame = useCallback(
    (gameId: string) => {
      onClose();
      router.push(`/library/${gameId}`);
    },
    [onClose, router]
  );

  const drawerTitle =
    step === 'manual'
      ? t('pages.library.addGame.manualTitle')
      : step === 'catalog'
        ? t('pages.library.addGame.catalogTitle')
        : t('pages.library.addGame.drawerTitle');

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetContent
        side="right"
        className="w-full sm:max-w-xl flex flex-col p-0"
        data-testid="add-game-drawer"
      >
        <SheetHeader className="px-6 py-4 border-b border-border/50">
          <SheetTitle data-testid="add-game-drawer-title">{drawerTitle}</SheetTitle>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto">
          {/* Step 0: Choice */}
          {step === 'choice' && (
            <div className="px-6 py-6 space-y-4" data-testid="add-game-step-choice">
              <p className="text-sm text-muted-foreground">{t('pages.library.addGame.question')}</p>

              <ChoiceCard
                data-testid="add-game-choice-manual"
                accent="game"
                glyph="✍️"
                title={t('pages.library.addGame.manualLabel')}
                description={t('pages.library.addGame.manualDescription')}
                onClick={() => {
                  trackEvent('library_addgame_choice_selected', { choice: 'manual' });
                  setStep('manual');
                }}
              />

              <ChoiceCard
                data-testid="add-game-choice-catalog"
                accent="kb"
                glyph="📚"
                title={t('pages.library.addGame.catalogLabel')}
                description={t('pages.library.addGame.catalogDescription')}
                onClick={() => {
                  trackEvent('library_addgame_choice_selected', { choice: 'catalog' });
                  setStep('catalog');
                }}
              />
            </div>
          )}

          {/* Step 1a: Manual wizard (compact: 1-step game creation only) */}
          {step === 'manual' && (
            <div data-testid="add-game-step-manual">
              <UserWizardClient
                compactMode
                onComplete={onClose}
                onCancel={() => setStep('choice')}
              />
            </div>
          )}

          {/* Step 1b: Catalog search (1-click add → redirect to detail) */}
          {step === 'catalog' && (
            <div data-testid="add-game-step-catalog">
              <CatalogSearchStep
                onSelect={handleNavigateToGame}
                onBack={() => setStep('choice')}
                // #2269 P0-1 (M1) — bridge for empty-state CTA so users searching
                // a game that does not exist in the catalog can switch to manual
                // creation without closing the drawer.
                onGoToManual={() => setStep('manual')}
                // #2269 P0-2 (M2) — blocked-alert "Vai alla scheda" CTA jumps
                // to the existing game's detail page (close drawer + push).
                onNavigateToGame={handleNavigateToGame}
              />
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}

// ─── URL-aware wrapper ────────────────────────────────────────────────────────

/**
 * AddGameDrawerController — reads ?action=add from URL and drives open state.
 * Mount once in _content.tsx; it manages its own open/close via router.
 */
export function AddGameDrawerController() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const action = searchParams.get('action');
  const isOpen = action === 'add';

  const handleClose = useCallback(() => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete('action');
    const newUrl = params.toString() ? `/library?${params.toString()}` : '/library';
    router.replace(newUrl);
  }, [router, searchParams]);

  return <AddGameDrawer open={isOpen} onClose={handleClose} />;
}
