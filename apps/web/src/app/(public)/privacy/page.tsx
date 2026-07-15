/**
 * Privacy Policy Page
 *
 * GDPR-compliant privacy policy with AI data processing disclosure.
 * Uses shared LegalPageLayout with:
 * - Rich markdown content (tables, lists)
 * - IT/EN language toggle
 * - JSON-LD structured data for SEO
 * - AI privacy sections (GDPR Art. 13/14)
 */

import { LegalPageLayout } from '@/components/legal';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Privacy Policy | MeepleAI',
  description:
    'How MeepleAI collects, uses, and protects your personal data. GDPR-compliant privacy policy including AI data processing disclosure.',
  openGraph: {
    title: 'Privacy Policy | MeepleAI',
    description:
      'How MeepleAI collects, uses, and protects your personal data. GDPR-compliant privacy policy.',
    url: 'https://meepleai.com/privacy',
    type: 'website',
  },
  alternates: {
    canonical: 'https://meepleai.com/privacy',
  },
};

const PRIVACY_SECTIONS = [
  'introduction',
  'dataController',
  'dataCollected',
  'purposes',
  'legalBasis',
  'dataSharing',
  'retention',
  'userRights',
  'cookies',
  'security',
  'transfers',
  'changes',
  'contact',
  // AI-specific GDPR disclosure sections (Art. 13/14)
  'aiProcessing',
  'aiProviders',
  'aiDataProtection',
  'aiRetention',
  'aiRights',
  'aiLegalBasis',
] as const;

export default function PrivacyPage() {
  return (
    <LegalPageLayout
      pageKey="privacy"
      sections={PRIVACY_SECTIONS}
      defaultOpenSection="introduction"
      lastUpdated={new Date('2026-03-09')}
      nextLink={{
        href: '/terms',
        labelIt: 'Termini e Condizioni',
        labelEn: 'Terms and Conditions',
      }}
    />
  );
}
