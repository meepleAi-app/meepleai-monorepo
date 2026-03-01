/**
 * LibraryEmptyState — Inviting empty-state for private library with 0 games.
 * Issue #4945: Player Journey — empty state with onboarding CTA.
 */

'use client';

import { Gamepad2, Search, Sparkles } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

interface LibraryEmptyStateProps {
  /** Custom class for wrapper */
  className?: string;
}

/**
 * Decorative meeple cluster shown when a user's private library is empty.
 * Guides new players toward the first action in the Player Journey.
 */
export function LibraryEmptyState({ className }: LibraryEmptyStateProps) {
  const { t } = useTranslation();

  return (
    <div
      role="region"
      aria-label={t('privateGames.noGamesYet')}
      className={`flex flex-col items-center justify-center py-16 ${className ?? ''}`}
      data-testid="empty-state"
    >
      {/* Decorative meeple cluster */}
      <div className="flex items-end gap-2 mb-8 opacity-25 select-none" aria-hidden="true">
        <Gamepad2 className="h-8 w-8 text-orange-500 -rotate-12" />
        <Gamepad2 className="h-12 w-12 text-orange-400" />
        <Sparkles className="h-6 w-6 text-orange-300 mb-1" />
        <Gamepad2 className="h-10 w-10 text-orange-500 rotate-6" />
        <Gamepad2 className="h-7 w-7 text-orange-400 rotate-12" />
      </div>

      {/* Main card */}
      <div
        className={[
          'max-w-lg w-full rounded-xl border-2 border-dashed border-orange-500/30',
          'bg-card/50 backdrop-blur-sm px-8 py-10 text-center space-y-4',
          'dark:bg-card/40',
        ].join(' ')}
      >
        {/* Title */}
        <h2 className="text-xl font-semibold tracking-tight text-foreground">
          {t('privateGames.noGamesYet')}
        </h2>

        {/* Description */}
        <p className="text-muted-foreground text-sm leading-relaxed">
          {t('privateGames.emptyStateDescription')}
        </p>

        {/* Primary CTA */}
        <div className="pt-2 flex flex-col sm:flex-row gap-3 justify-center">
          <Button
            asChild
            className="bg-orange-500 hover:bg-orange-600 text-white font-medium w-full sm:w-auto"
            data-testid="empty-state-primary-cta"
          >
            <Link href="/library?action=add">
              <span className="mr-1 font-bold">+</span> {t('privateGames.addFirstGame')}
            </Link>
          </Button>

          {/* Secondary CTA */}
          <Button
            asChild
            variant="outline"
            className="w-full sm:w-auto"
            data-testid="empty-state-secondary-cta"
          >
            <Link href="/library?tab=collection">
              <Search className="h-4 w-4 mr-2" />
              {t('privateGames.browseCatalog')}
            </Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
