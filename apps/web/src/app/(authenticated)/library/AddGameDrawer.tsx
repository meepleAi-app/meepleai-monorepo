'use client';

/**
 * AddGameDrawer — Right-side Sheet for adding a game to the personal library
 * Issue #5168 — AddGameDrawer wizard with Step 0 (method choice) + Step 1a (manual form)
 * Issue #5169 — Step 1b: CatalogSearchStep (search shared catalog + add to library)
 *
 * Flow:
 *   Step 0: Choose method — "Manually" or "From Catalog"
 *   Step 1a (Manual):  Embed UserWizardClient (3-step: game → PDF → agent)
 *   Step 1b (Catalog): CatalogSearchStep → select game → add to library → advance to PDF step
 *   Step 2  (Catalog PDF): UserWizardClient starting at PDF upload (gameId pre-set)
 *
 * URL integration:
 *   ?action=add         → drawer opens (choice step)
 *   ?action=import-bgg  → drawer opens (BGG search step) — Issue #373
 *   close / ESC         → removes ?action from URL
 */

import { useCallback, useEffect, useState } from 'react';

import { BookOpen, Globe, PenLine } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { CatalogSearchStep } from '@/app/(authenticated)/library/CatalogSearchStep';
import { UserWizardClient } from '@/app/(authenticated)/library/private/add/client';
import { BggSearchPanel } from '@/components/games/BggSearchPanel';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';

// ─── Types ────────────────────────────────────────────────────────────────────

type DrawerStep = 'choice' | 'manual' | 'catalog' | 'catalog-pdf' | 'bgg';

interface CatalogSelection {
  gameId: string;
  gameName: string;
}

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
  /** Issue #373: Allow opening directly to a specific step (e.g., 'bgg' from URL) */
  initialStep?: DrawerStep;
}

export function AddGameDrawer({ open, onClose, initialStep }: AddGameDrawerProps) {
  const router = useRouter();
  const [step, setStep] = useState<DrawerStep>(initialStep ?? 'choice');
  const [catalogSelection, setCatalogSelection] = useState<CatalogSelection | null>(null);

  // Sync step when initialStep prop changes (e.g., URL switches from ?action=add to ?action=import-bgg)
  useEffect(() => {
    if (open && initialStep) {
      setStep(initialStep);
    }
  }, [open, initialStep]);

  // Reset to choice step after close animation finishes
  const handleOpenChange = useCallback(
    (isOpen: boolean) => {
      if (!isOpen) {
        onClose();
        setTimeout(() => {
          setStep('choice');
          setCatalogSelection(null);
        }, 300);
      }
    },
    [onClose]
  );

  // Called by CatalogSearchStep after game is successfully added to library
  const handleCatalogSelect = useCallback((gameId: string, gameName: string) => {
    setCatalogSelection({ gameId, gameName });
    setStep('catalog-pdf');
  }, []);

  const drawerTitle =
    step === 'manual'
      ? 'Add game manually'
      : step === 'catalog' || step === 'catalog-pdf'
        ? 'Add from catalog'
        : step === 'bgg'
          ? 'Import from BoardGameGeek'
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
                description="Enter game details yourself and optionally upload the rulebook PDF."
                onClick={() => setStep('manual')}
              />

              <ChoiceCard
                data-testid="add-game-choice-catalog"
                icon={<BookOpen className="h-6 w-6" />}
                title="From shared catalog"
                description="Search the community catalog and add a game to your personal library."
                onClick={() => setStep('catalog')}
              />

              <ChoiceCard
                data-testid="add-game-choice-bgg"
                icon={<Globe className="h-6 w-6" />}
                title="Import from BoardGameGeek"
                description="Search BGG and import a game as a private game in your library."
                onClick={() => setStep('bgg')}
              />
            </div>
          )}

          {/* Step 1a: Manual wizard */}
          {step === 'manual' && (
            <div data-testid="add-game-step-manual">
              <UserWizardClient onComplete={onClose} onCancel={() => setStep('choice')} />
            </div>
          )}

          {/* Step 1b: Catalog search */}
          {step === 'catalog' && (
            <div data-testid="add-game-step-catalog">
              <CatalogSearchStep onSelect={handleCatalogSelect} onBack={() => setStep('choice')} />
            </div>
          )}

          {/* Step: BGG search & import (Issue #373) */}
          {step === 'bgg' && (
            <div className="px-6 py-6" data-testid="add-game-step-bgg">
              <BggSearchPanel
                onImportSuccess={result => {
                  // After import, navigate to the new private game
                  onClose();
                  router.push(`/library/private/${result.privateGameId}`);
                }}
              />
            </div>
          )}

          {/* Step 2 (after catalog select): PDF upload wizard */}
          {step === 'catalog-pdf' && catalogSelection && (
            <div data-testid="add-game-step-catalog-pdf">
              <UserWizardClient
                gameId={catalogSelection.gameId}
                gameName={catalogSelection.gameName}
                startAtPdf
                onComplete={() => {
                  onClose();
                  router.push(`/library/games/${catalogSelection.gameId}`);
                }}
                onCancel={() => setStep('catalog')}
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
 * AddGameDrawerController — reads ?action=add|import-bgg from URL and drives open state.
 * Mount once in _content.tsx; it manages its own open/close via router.
 * Issue #373: Also handles ?action=import-bgg to open directly to BGG search step.
 */
export function AddGameDrawerController() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const action = searchParams.get('action');
  const isOpen = action === 'add' || action === 'import-bgg';
  const initialStep: DrawerStep | undefined = action === 'import-bgg' ? 'bgg' : undefined;

  const handleClose = useCallback(() => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete('action');
    const newUrl = params.toString() ? `/library?${params.toString()}` : '/library';
    router.replace(newUrl);
  }, [router, searchParams]);

  return <AddGameDrawer open={isOpen} onClose={handleClose} initialStep={initialStep} />;
}
