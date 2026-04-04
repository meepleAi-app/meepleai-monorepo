'use client';

/**
 * InterestsStep Component
 * Issue #132 - Category interests selection during onboarding
 *
 * Checkbox grid with 9 board game category options.
 * Selections can be used for personalized recommendations.
 */

import { FormEvent, useState } from 'react';

import { toast } from 'sonner';

import { AccessibleButton } from '@/components/accessible';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface InterestsStepProps {
  onComplete: () => void;
  onSkip: () => void;
}

const INTEREST_CATEGORIES = [
  { id: 'strategy', label: 'Strategy', icon: '\u265F' },
  { id: 'party', label: 'Party', icon: '\u{1F389}' },
  { id: 'cooperative', label: 'Cooperative', icon: '\u{1F91D}' },
  { id: 'family', label: 'Family', icon: '\u{1F3E0}' },
  { id: 'thematic', label: 'Thematic', icon: '\u{1F3AD}' },
  { id: 'abstract', label: 'Abstract', icon: '\u25B3' },
  { id: 'card', label: 'Card', icon: '\u{1F0CF}' },
  { id: 'dice', label: 'Dice', icon: '\u{1F3B2}' },
  { id: 'miniatures', label: 'Miniatures', icon: '\u2694\uFE0F' },
] as const;

export function InterestsStep({ onComplete, onSkip }: InterestsStepProps) {
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
      toast.success(`${selected.size} interests saved!`);
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
        <h2 className="font-quicksand text-lg font-semibold text-slate-900">What Do You Enjoy?</h2>
        <p className="mt-1 text-sm text-slate-600">
          Select categories that interest you. This helps us personalize your experience.
        </p>
      </div>

      <form noValidate onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-3 gap-3" role="group" aria-label="Game category interests">
          {INTEREST_CATEGORIES.map(cat => {
            const isSelected = selected.has(cat.id);
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
                    : 'border-slate-200 bg-white text-slate-700'
                )}
                data-testid={`interest-${cat.id}`}
              >
                <span className="text-2xl" aria-hidden="true">
                  {cat.icon}
                </span>
                <span className="text-sm font-medium">{cat.label}</span>
              </button>
            );
          })}
        </div>

        {selected.size > 0 && (
          <p className="text-center text-sm text-slate-500">{selected.size} selected</p>
        )}

        <div className="flex items-center gap-3">
          <AccessibleButton
            type="submit"
            variant="primary"
            className="flex-1"
            isLoading={isLoading}
            loadingText="Saving..."
          >
            {selected.size > 0 ? 'Continue' : 'Skip'}
          </AccessibleButton>
          <button
            type="button"
            onClick={onSkip}
            className="text-sm text-slate-500 hover:text-slate-700"
            data-testid="interests-skip"
          >
            Skip for now
          </button>
        </div>
      </form>
    </div>
  );
}
