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
        'hover:border-orange-500/60 hover:bg-orange-50/50 dark:hover:bg-orange-950/20',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500',
        'transition-colors cursor-pointer',
      ].join(' ')}
    >
      <div className="flex items-start gap-4">
        <div className="flex-shrink-0 mt-0.5 text-orange-500">{icon}</div>
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
  const [step, setStep] = useState<DrawerStep>('choice');
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    };
  }, []);

  // Reset to choice step after close animation finishes
  const handleOpenChange = useCallback(
    (isOpen: boolean) => {
      if (!isOpen) {
        onClose();
        closeTimerRef.current = setTimeout(() => {
          setStep('choice');
        }, 300);
      }
    },
    [onClose]
  );

  // Called by CatalogSearchStep after game is successfully added to library.
  // Simplified flow: no longer transitions to a PDF step — close drawer and
  // jump straight to the game detail page where PDF/Agent CTAs await.
  const handleCatalogSelect = useCallback(
    (gameId: string, _gameName: string) => {
      onClose();
      router.push(`/library/${gameId}`);
    },
    [onClose, router]
  );

  const drawerTitle =
    step === 'manual'
      ? 'Add game manually'
      : step === 'catalog'
        ? 'Add from catalog'
        : 'Add a game';

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
              <p className="text-sm text-muted-foreground">How do you want to add your game?</p>

              <ChoiceCard
                data-testid="add-game-choice-manual"
                icon={<PenLine className="h-6 w-6" />}
                title="Add manually"
                description="Enter the game details. You can upload the rulebook and configure the AI agent later from the game detail page."
                onClick={() => setStep('manual')}
              />

              <ChoiceCard
                data-testid="add-game-choice-catalog"
                icon={<BookOpen className="h-6 w-6" />}
                title="From shared catalog"
                description="Search the community catalog and add a game in one click. Rulebook and AI agent setup come later."
                onClick={() => setStep('catalog')}
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
              <CatalogSearchStep onSelect={handleCatalogSelect} onBack={() => setStep('choice')} />
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
