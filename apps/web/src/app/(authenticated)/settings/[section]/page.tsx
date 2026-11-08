/**
 * `/settings/[section]` — user settings hub, addressed section (#3938).
 *
 * An unknown section falls back to the default instead of 404-ing. That is a
 * deliberate choice, not laziness: the backend emits section links that do not
 * all correspond to an implemented section — `NotificationRoutes` has
 * `/settings/subscription`, and there is no `subscription` section in
 * `SETTINGS_SECTIONS`. Landing such a link on the hub is better than landing it
 * on a 404, and the gap is tracked separately.
 */

import type { JSX } from 'react';

import { DEFAULT_SECTION, isValidSection } from '@/components/features/settings/settings-sections';

import { SettingsPageContent } from '../_components/SettingsPageContent';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Impostazioni | MeepleAI',
  description: 'Profilo, sicurezza, consenso AI, preferenze e chiavi API.',
};

export default async function SettingsSectionPage({
  params,
}: {
  params: Promise<{ section: string }>;
}): Promise<JSX.Element> {
  const { section } = await params;
  const activeSection = isValidSection(section) ? section : DEFAULT_SECTION;

  return <SettingsPageContent activeSection={activeSection} />;
}
