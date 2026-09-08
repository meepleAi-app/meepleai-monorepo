/**
 * `/settings` — user settings hub, default section (#3938).
 *
 * Renders the default section directly instead of redirecting to
 * `/settings/profile`: see `SettingsPageContent` for why this route must
 * resolve without a redirect.
 */

import type { JSX } from 'react';

import { DEFAULT_SECTION } from '@/components/features/settings/settings-sections';

import { SettingsPageContent } from './_components/SettingsPageContent';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Impostazioni | MeepleAI',
  description: 'Profilo, sicurezza, consenso AI, preferenze e chiavi API.',
};

export default function SettingsPage(): JSX.Element {
  return <SettingsPageContent activeSection={DEFAULT_SECTION} />;
}
