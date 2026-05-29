/**
 * EditGateBanner — K5 gate explanation + delete CTA
 *
 * Displays banner explaining K5 limitation (readonly fields)
 * and provides quick-access delete button.
 *
 * AC-4.4: Banner inline sopra form con "Cancella partita" link
 * Issue #1488: Play Records reskin — Task 4
 */

'use client';

import { AlertTriangle } from 'lucide-react';
import { useTranslations } from 'next-intl';

import { Button } from '@/components/ui/primitives/button';

export interface EditGateBannerProps {
  onDelete: () => void;
  isDeleting?: boolean;
}

export function EditGateBanner({ onDelete, isDeleting = false }: EditGateBannerProps) {
  const t = useTranslations('playRecords.edit');

  return (
    <div
      role="alert"
      aria-live="polite"
      className="rounded-md bg-entity-toolkit/10 border border-entity-toolkit/30 p-4 mb-6"
    >
      <div className="flex gap-3">
        {/* Icon */}
        <div className="flex-shrink-0 mt-0.5">
          <AlertTriangle className="h-5 w-5 text-entity-toolkit" aria-hidden="true" />
        </div>

        {/* Content */}
        <div className="flex-1 space-y-2">
          <h3 className="font-semibold text-foreground">{t('banner.gateTitle')}</h3>
          <p className="text-sm text-muted-foreground">{t('banner.gateDescription')}</p>

          {/* Delete CTA */}
          <div className="pt-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onDelete}
              disabled={isDeleting}
              aria-label={t('banner.deleteAction')}
            >
              {isDeleting ? `${t('actions.delete')}…` : t('banner.deleteAction')}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
