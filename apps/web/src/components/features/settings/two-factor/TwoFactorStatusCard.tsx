'use client';

import React from 'react';

import { Shield, ShieldCheck, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

interface Props {
  readonly status: {
    isEnabled: boolean;
    enabledAt: string | null;
    unusedBackupCodesCount: number;
  };
  readonly onSetup: () => void;
  readonly onDisable: () => void;
  readonly isPending?: boolean;
}

export function TwoFactorStatusCard({
  status,
  onSetup,
  onDisable,
  isPending,
}: Props): React.JSX.Element {
  const { t, locale } = useTranslation();
  const enabled = status.isEnabled;
  return (
    <section className="bg-card border border-border rounded-lg p-5">
      <header className="flex items-start gap-3">
        <div className="w-9 h-9 rounded-md bg-entity-kb/10 text-entity-kb flex items-center justify-center shrink-0">
          {enabled ? (
            <ShieldCheck className="h-5 w-5" aria-hidden />
          ) : (
            <Shield className="h-5 w-5" aria-hidden />
          )}
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="font-quicksand font-bold text-foreground">
            {t('settings.security.statusCard.title')}
          </h3>
          <p
            data-testid="2fa-status"
            className={
              enabled
                ? 'inline-flex items-center gap-1.5 mt-1 text-xs font-mono font-bold uppercase tracking-wide bg-[hsl(var(--c-success)/0.15)] text-[hsl(var(--c-success-ink))] px-2 py-0.5 rounded-sm'
                : 'inline-flex items-center gap-1.5 mt-1 text-xs font-mono font-bold uppercase tracking-wide bg-[hsl(var(--c-warning)/0.15)] text-[hsl(var(--c-warning-ink))] px-2 py-0.5 rounded-sm'
            }
          >
            {enabled ? (
              <>
                ✓ {t('settings.security.statusCard.enabled')}
                {status.enabledAt &&
                  ` · ${new Date(status.enabledAt).toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' })}`}
              </>
            ) : (
              <>
                <AlertCircle className="h-3 w-3" aria-hidden />{' '}
                {t('settings.security.statusCard.notEnabled')}
              </>
            )}
          </p>
        </div>
      </header>

      <p className="text-sm text-muted-foreground mt-3">
        {enabled
          ? t('settings.security.statusCard.enabledDescription')
          : t('settings.security.statusCard.disabledDescription')}
      </p>

      {!enabled && (
        <Button data-testid="enable-2fa" className="mt-4" onClick={onSetup} disabled={isPending}>
          {t('settings.security.statusCard.setUp')}
        </Button>
      )}
      {enabled && (
        <div className="flex gap-2 mt-4 flex-wrap">
          <Button
            variant="outline"
            size="sm"
            disabled
            title={t('settings.security.statusCard.regenerateCodesSoon')}
          >
            {t('settings.security.statusCard.regenerateCodes')}
          </Button>
          <Button
            data-testid="disable-2fa"
            variant="destructive"
            size="sm"
            onClick={onDisable}
            disabled={isPending}
          >
            {t('settings.security.statusCard.disable')}
          </Button>
        </div>
      )}
    </section>
  );
}
