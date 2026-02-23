'use client';

/**
 * AddGameDrawer — Right-side Sheet for adding a game to the personal library
 * Issue #5168 — AddGameDrawer wizard with Step 0 (method choice) + Step 1a (manual form)
 *
 * Flow:
 *   Step 0: Choose method — "Manually" or "From Catalog"
 *   Step 1a (Manual): Embed UserWizardClient (3-step: game → PDF → agent)
 *   Step 1b (Catalog): Issue #5169 (CatalogSearchStep — not yet implemented)
 *
 * URL integration:
 *   ?action=add     → drawer opens
 *   close / ESC     → removes ?action=add from URL
 */

import { useCallback, useState } from 'react';

import { BookOpen, PenLine } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { UserWizardClient } from '@/app/(authenticated)/library/private/add/client';
import { Button } from '@/components/ui/primitives/button';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';

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

// ─── Step 1b placeholder ──────────────────────────────────────────────────────

function CatalogSearchStep({ onBack }: { onBack: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4">
      <BookOpen className="h-12 w-12 text-muted-foreground/40" />
      <p className="text-muted-foreground text-sm">
        Catalog search — coming in Issue #5169.
      </p>
      <Button variant="outline" size="sm" onClick={onBack}>
        Back
      </Button>
    </div>
  );
}

// ─── Main drawer ──────────────────────────────────────────────────────────────

interface AddGameDrawerProps {
  open: boolean;
  onClose: () => void;
}

export function AddGameDrawer({ open, onClose }: AddGameDrawerProps) {
  const [step, setStep] = useState<DrawerStep>('choice');

  // Reset to choice step whenever the drawer opens
  const handleOpenChange = useCallback(
    (isOpen: boolean) => {
      if (!isOpen) {
        onClose();
        // Reset step after close animation finishes
        setTimeout(() => setStep('choice'), 300);
      }
    },
    [onClose],
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
              <p className="text-sm text-muted-foreground">
                How do you want to add your game?
              </p>

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
            </div>
          )}

          {/* Step 1a: Manual wizard */}
          {step === 'manual' && (
            <div data-testid="add-game-step-manual">
              <UserWizardClient onComplete={onClose} onCancel={() => setStep('choice')} />
            </div>
          )}

          {/* Step 1b: Catalog search (Issue #5169) */}
          {step === 'catalog' && (
            <CatalogSearchStep onBack={() => setStep('choice')} />
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
  const isOpen = searchParams.get('action') === 'add';

  const handleClose = useCallback(() => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete('action');
    const newUrl = params.toString() ? `/library?${params.toString()}` : '/library';
    router.replace(newUrl);
  }, [router, searchParams]);

  return <AddGameDrawer open={isOpen} onClose={handleClose} />;
}
