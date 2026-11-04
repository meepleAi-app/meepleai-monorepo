'use client';

import type { JSX } from 'react';

import { useRouter } from 'next/navigation';

import { type SettingsSectionId } from '@/components/features/settings/settings-sections';
import { SettingsTab } from '@/components/features/settings/SettingsTab';
import { HubPageContainer } from '@/components/layout/PageContainer';

interface Props {
  readonly activeSection: SettingsSectionId;
}

/**
 * `/settings/[section]` — canonical address of the user settings hub (#3938).
 *
 * The hub is not new: `SettingsTab` and its sections already existed, reachable
 * at `/profile?tab=settings&section=<id>`. What was missing was the address.
 *
 * **Why sub-routes and not `?section=`.** Two independent reasons:
 *
 * 1. ADR-091 decided sub-route over query param for the game detail tree, and
 *    the same criterion applies here.
 * 2. The backend already emits sub-route links, and they were broken:
 *    `EmailTemplateService.WrapInBaseTemplate` puts
 *    `{frontendBaseUrl}/settings/notifications` in the footer of **every**
 *    email, and `NotificationRoutes.SettingsSubscription` is
 *    `/settings/subscription`. With `?section=` those would have stayed 404.
 *
 * `/settings` itself must NOT redirect: `gotoChecked` in
 * `e2e/accessibility.spec.ts` (#3917) asserts the landed pathname equals the
 * requested one, so a bridge redirect would break the a11y test #3940 re-enables.
 * The redirect goes the other way — `/profile?tab=settings` → here.
 */
export function SettingsPageContent({ activeSection }: Props): JSX.Element {
  const router = useRouter();

  return (
    <HubPageContainer className="py-8">
      <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground mb-6">
        Impostazioni
      </h1>
      <SettingsTab
        activeSection={activeSection}
        onChangeSection={id => router.push(`/settings/${id}`)}
      />
    </HubPageContainer>
  );
}
