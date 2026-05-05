'use client';

import { useCallback, useEffect, useState } from 'react';

import Link from 'next/link';
import { toast } from 'sonner';

import { Btn } from '@/components/ui/v2/btn';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';
import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';
import { useTranslation } from '@/hooks/useTranslation';
import { getStoredConsent, setStoredConsent } from '@/lib/cookie-consent';

export default function CookieSettingsPage() {
  const { t } = useTranslation();
  const [analytics, setAnalytics] = useState(false);
  const [functional, setFunctional] = useState(false);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    const stored = getStoredConsent();
    if (stored) {
      setAnalytics(stored.analytics);
      setFunctional(stored.functional);
    }
    setHydrated(true);
  }, []);

  const save = useCallback(
    (next: { analytics: boolean; functional: boolean }) => {
      setStoredConsent(next);
      setAnalytics(next.analytics);
      setFunctional(next.functional);
      toast.success(t('pages.cookieSettings.savedToast'));
      window.dispatchEvent(new CustomEvent('cookie-consent-updated'));
    },
    [t]
  );

  return (
    <main>
      <HeroGradient
        title={t('pages.cookieSettings.title')}
        subtitle={t('pages.cookieSettings.subtitle')}
      />

      <section className="max-w-2xl mx-auto py-8 px-4">
        <SettingsList>
          <SettingsRow
            label={t('pages.cookieSettings.categories.essential.label')}
            description={t('pages.cookieSettings.categories.essential.description')}
            trailing={
              <ToggleSwitch
                checked={true}
                disabled
                onCheckedChange={() => {}}
                ariaLabel={t('pages.cookieSettings.categories.essential.label')}
              />
            }
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.analytics.label')}
            description={t('pages.cookieSettings.categories.analytics.description')}
            trailing={
              <ToggleSwitch
                checked={analytics}
                onCheckedChange={setAnalytics}
                disabled={!hydrated}
                ariaLabel={t('pages.cookieSettings.categories.analytics.label')}
              />
            }
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.functional.label')}
            description={t('pages.cookieSettings.categories.functional.description')}
            trailing={
              <ToggleSwitch
                checked={functional}
                onCheckedChange={setFunctional}
                disabled={!hydrated}
                ariaLabel={t('pages.cookieSettings.categories.functional.label')}
              />
            }
          />
        </SettingsList>

        <div className="flex flex-col sm:flex-row gap-3 mt-6">
          <Btn
            variant="primary"
            onClick={() => save({ analytics, functional })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.save')}
          </Btn>
          <Btn
            variant="ghost"
            onClick={() => save({ analytics: true, functional: true })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.acceptAll')}
          </Btn>
          <Btn
            variant="ghost"
            onClick={() => save({ analytics: false, functional: false })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.rejectAll')}
          </Btn>
        </div>

        <div className="mt-8 text-center">
          <Link href="/cookies" className="text-sm underline">
            {t('pages.cookieSettings.policyLink')}
          </Link>
        </div>
      </section>
    </main>
  );
}
