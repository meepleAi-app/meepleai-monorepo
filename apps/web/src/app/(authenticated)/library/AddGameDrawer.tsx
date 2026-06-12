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

import { BookOpen, PenLine } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { CatalogSearchStep } from '@/app/(authenticated)/library/CatalogSearchStep';
import { UserWizardClient } from '@/app/(authenticated)/library/private/add/client';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

// ─── Types ────────────────────────────────────────────────────────────────────

type DrawerStep = 'choice' | 'manual' | 'catalog';

// ─── Step 0: Choice cards ─────────────────────────────────────────────────────

interface ChoiceCardProps {
  icon: React.ReactNode;
  title: string;
  description: string;
  onClick: () => void;
  'data-testid'?: string;
}

function ChoiceCard({ icon, title, description, onClick, 'data-testid': testId }: ChoiceCardProps) {
  return (
    <button
      type="button"
      data-testid={testId}
      onClick={onClick}
      className={[
        'w-full text-left rounded-xl border-2 border-border/50 p-5',
        // #2269 P0-4 — token canonical: orange-* hardcoded → entity-game.
        // The entity utility resolves to --c-game and is theme-aware (light
        // + dark), removing the need for `dark:` variants.
        'hover:border-entity-game/60 hover:bg-entity-game/10',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-game',
        'transition-colors cursor-pointer',
      ].join(' ')}
    >
      <div className="flex items-start gap-4">
        {/*
          F2.2 T6 #1974 (audit 2026-06-07, a11y audit): icon is purely
          decorative — the title + description below already convey the
          choice. `aria-hidden` keeps screen readers from announcing
          "image, image" before the meaningful label.
        */}
        <div aria-hidden="true" className="flex-shrink-0 mt-0.5 text-entity-game">
          {icon}
        </div>
        <div>
          <p className="font-semibold text-foreground">{title}</p>
          <p className="text-sm text-muted-foreground mt-1">{description}</p>
        </div>
      </div>
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

  const handleCatalogSelect = useCallback(
    (gameId: string, _gameName: string) => {
      handleNavigateToGame(gameId);
    },
    [handleNavigateToGame]
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
        // #2269 P0-3 (M4) — Radix Dialog primitive does not emit aria-modal
        // on its own (see memory `radix-dialog-no-aria-modal`); we set it
        // explicitly so screen readers announce the drawer as a modal.
        aria-modal="true"
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
                icon={<PenLine className="h-6 w-6" />}
                title={t('pages.library.addGame.manualLabel')}
                description={t('pages.library.addGame.manualDescription')}
                onClick={() => {
                  trackEvent('library_addgame_choice_selected', { choice: 'manual' });
                  setStep('manual');
                }}
              />

              <ChoiceCard
                data-testid="add-game-choice-catalog"
                icon={<BookOpen className="h-6 w-6" />}
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
                onSelect={handleCatalogSelect}
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
