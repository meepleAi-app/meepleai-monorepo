/**
 * ManagePreferencesLink — client island for the /cookies footer.
 *
 * Isolates the `useTranslation` client boundary so that the parent
 * `cookies/page.tsx` can remain a server component (preserving the
 * `export const metadata` required by the App Router).
 */

'use client';

import Link from 'next/link';

import { useTranslation } from '@/hooks/useTranslation';

export function ManagePreferencesLink() {
  const { t } = useTranslation();

  return (
    <Link href="/cookie-settings" className="text-sm underline">
      {t('pages.cookies.managePreferencesCta')}
    </Link>
  );
}
