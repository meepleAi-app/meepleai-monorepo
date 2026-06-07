'use client';

/**
 * InterestsStep Component
 * Issue #132 - Category interests selection during onboarding
 *
 * Checkbox grid with 9 board game category options.
 * Selections can be used for personalized recommendations.
 *
 * F25 #1976 (audit 2026-06-07): all hardcoded English copy moved to
 * `pages.onboarding.interests.*` i18n keys (it.json + en.json) so the
 * wizard renders in Italian for the IT-default user base.
 */

import { FormEvent, useState } from 'react';

import { toast } from 'sonner';

import { AccessibleButton } from '@/components/accessible';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface InterestsStepProps {
  onComplete: () => void;
  onSkip: () => void;
}

/**
 * Category ids are kept stable (backend contract) but the rendered label
 * resolves through `pages.onboarding.interests.categories.{id}` at render
 * time, so adding a new locale doesn't touch this component.
 */
const INTEREST_CATEGORIES = [
  { id: 'strategy', icon: '♟' },
  { id: 'party', icon: '\u{1F389}' },
  { id: 'cooperative', icon: '\u{1F91D}' },
  { id: 'family', icon: '\u{1F3E0}' },
  { id: 'thematic', icon: '\u{1F3AD}' },
  { id: 'abstract', icon: '△' },
  { id: 'card', icon: '\u{1F0CF}' },
  { id: 'dice', icon: '\u{1F3B2}' },
  { id: 'miniatures', icon: '⚔️' },
] as const;

export function InterestsStep({ onComplete, onSkip }: InterestsStepProps) {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [isLoading, setIsLoading] = useState(false);

  const toggleCategory = (id: string) => {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (selected.size === 0) {
      onSkip();
      return;
    }

    setIsLoading(true);
    try {
      // Issue #323: Save interests to backend
      await api.auth.saveInterests(Array.from(selected));
      toast.success(t('pages.onboarding.interests.toast.saved', { count: selected.size }));
      onComplete();
    } catch {
      // Non-critical step, proceed anyway
      onComplete();
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground">
          {t('pages.onboarding.interests.heading')}
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          {t('pages.onboarding.interests.subtitle')}
        </p>
      </div>

      <form noValidate onSubmit={handleSubmit} className="space-y-6">
        <div
          className="grid grid-cols-3 gap-3"
          role="group"
          aria-label={t('pages.onboarding.interests.groupLabel')}
        >
          {INTEREST_CATEGORIES.map(cat => {
            const isSelected = selected.has(cat.id);
            const label = t(`pages.onboarding.interests.categories.${cat.id}`);
            return (
              <button
                key={cat.id}
                type="button"
                role="checkbox"
                aria-checked={isSelected}
                onClick={() => toggleCategory(cat.id)}
                className={cn(
                  'flex flex-col items-center gap-2 rounded-xl border-2 p-4 text-center transition-all',
                  'hover:border-amber-300 hover:bg-amber-50/50',
                  isSelected
                    ? 'border-amber-500 bg-amber-50 text-amber-900'
                    : 'border-border bg-card text-foreground'
                )}
                data-testid={`interest-${cat.id}`}
              >
                <span className="text-2xl" aria-hidden="true">
                  {cat.icon}
                </span>
                <span className="text-sm font-medium">{label}</span>
              </button>
            );
          })}
        </div>

        {selected.size > 0 && (
          <p className="text-center text-sm text-muted-foreground">
            {t('pages.onboarding.interests.selectedCount', { count: selected.size })}
          </p>
        )}

        <div className="flex items-center gap-3">
          <AccessibleButton
            type="submit"
            variant="primary"
            className="flex-1"
            isLoading={isLoading}
            loadingText={t('pages.onboarding.interests.loading')}
          >
            {selected.size > 0
              ? t('pages.onboarding.interests.cta.continue')
              : t('pages.onboarding.interests.cta.skip')}
          </AccessibleButton>
          <button
            type="button"
            onClick={onSkip}
            className="text-sm text-muted-foreground hover:text-foreground"
            data-testid="interests-skip"
          >
            {t('pages.onboarding.interests.cta.skipForNow')}
          </button>
        </div>
      </form>
    </div>
  );
}
