/**
 * Cookie Policy Page
 *
 * GDPR/ePrivacy-compliant cookie policy.
 * Uses shared LegalPageLayout with:
 * - Rich markdown content (tables, lists)
 * - IT/EN language toggle
 * - JSON-LD structured data for SEO
 */

import { LegalPageLayout } from '@/components/legal';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Cookie Policy | MeepleAI',
  description:
    'How MeepleAI uses cookies and similar technologies. Learn about essential, analytical, and functional cookies.',
  openGraph: {
    title: 'Cookie Policy | MeepleAI',
    description: 'How MeepleAI uses cookies and similar technologies.',
    url: 'https://meepleai.com/cookies',
    type: 'website',
  },
  alternates: {
    canonical: 'https://meepleai.com/cookies',
  },
};

const COOKIE_SECTIONS = [
  'whatAreCookies',
  'typesOfCookies',
  'technicalCookies',
  'analyticalCookies',
  'functionalCookies',
  'thirdPartyCookies',
  'cookieManagement',
  'cookieDuration',
  'policyUpdates',
] as const;

export default function CookiesPage() {
  return (
    <LegalPageLayout
      pageKey="cookies"
      sections={COOKIE_SECTIONS}
      defaultOpenSection="whatAreCookies"
      lastUpdated={new Date('2026-03-09')}
      prevLink={{
        href: '/terms',
        labelIt: 'Termini e Condizioni',
        labelEn: 'Terms and Conditions',
      }}
    />
  );
}
