/**
 * Terms of Service Page
 *
 * Legal terms and conditions with AI usage, copyright, and DMCA provisions.
 * Uses shared LegalPageLayout with:
 * - Rich markdown content (tables, lists)
 * - IT/EN language toggle
 * - JSON-LD structured data for SEO
 */

import { LegalPageLayout } from '@/components/legal';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Terms of Service | MeepleAI',
  description:
    'Terms and conditions for using MeepleAI, the AI-powered board game rules assistant. Includes AI usage policy, copyright, and user content guidelines.',
  openGraph: {
    title: 'Terms of Service | MeepleAI',
    description:
      'Terms and conditions for using MeepleAI, the AI-powered board game rules assistant.',
    url: 'https://meepleai.com/terms',
    type: 'website',
  },
  alternates: {
    canonical: 'https://meepleai.com/terms',
  },
};

const TERMS_SECTIONS = [
  'acceptance',
  'serviceDescription',
  'freemiumModel',
  'userAccount',
  'userContent',
  'aiUsage',
  'intellectualProperty',
  'limitations',
  'termination',
  'modifications',
  'governingLaw',
  'contact',
] as const;

export default function TermsPage() {
  return (
    <LegalPageLayout
      pageKey="terms"
      sections={TERMS_SECTIONS}
      defaultOpenSection="acceptance"
      lastUpdated={new Date('2026-03-09')}
      prevLink={{
        href: '/privacy',
        labelIt: 'Informativa Privacy',
        labelEn: 'Privacy Policy',
      }}
      nextLink={{
        href: '/cookies',
        labelIt: 'Cookie Policy',
        labelEn: 'Cookie Policy',
      }}
    />
  );
}
