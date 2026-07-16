'use client';

import type { JSX } from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

export interface GuideProgressProps {
  gameTitle: string;
  estimatedMinutes: number;
  completedCount: number;
  totalCount: number;
  confidence: number | null;
  canReset: boolean;
  onReset: () => void;
}

/**
 * Setup-guide progress card: title, estimated time, reset action,
 * progress bar/percentage, completion banner and AI-confidence badge.
 * Issue: fix/setup-page-redesign.
 */
export function GuideProgress({
  gameTitle,
  estimatedMinutes,
  completedCount,
  totalCount,
  confidence,
  canReset,
  onReset,
}: GuideProgressProps): JSX.Element {
  const { t } = useTranslation();
  const percentage = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;
  const isComplete = percentage === 100;

  return (
    <div className="rounded-lg border border-border bg-card p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold text-foreground">{gameTitle}</h2>
          <p className="text-sm text-muted-foreground">
            {t('pages.setup.estimatedTime', { minutes: estimatedMinutes })}
          </p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={onReset} disabled={!canReset}>
          {t('pages.setup.resetProgress')}
        </Button>
      </div>

      <div className="mt-4 space-y-2">
        <div className="flex items-center justify-between text-sm font-medium">
          <span className="text-foreground">
            {t('pages.setup.progress', { done: completedCount, total: totalCount })}
          </span>
          <span className={cn('text-foreground', isComplete && 'text-[hsl(var(--c-success-ink))]')}>
            {percentage}%
          </span>
        </div>
        <Progress
          value={percentage}
          aria-label={t('pages.setup.progress', { done: completedCount, total: totalCount })}
        />
      </div>

      {isComplete && (
        <div
          role="status"
          aria-live="polite"
          className="mt-4 flex items-center gap-3 rounded-md border border-[hsl(var(--c-success)/0.4)] bg-[hsl(var(--c-success)/0.1)] p-4 text-sm text-[hsl(var(--c-success-ink))]"
        >
          <span aria-hidden="true" className="text-2xl">
            🎉
          </span>
          <span>
            <strong>{t('pages.setup.complete.title')}</strong> {t('pages.setup.complete.body')}
          </span>
        </div>
      )}

      {confidence !== null && (
        <Badge variant="outline" className="mt-3">
          {t('pages.setup.aiConfidence', { percent: Math.round(confidence * 100) })}
        </Badge>
      )}
    </div>
  );
}
