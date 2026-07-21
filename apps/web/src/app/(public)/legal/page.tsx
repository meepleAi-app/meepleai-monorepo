/**
 * Legal Hub Page — /legal (#564, route-gap-analysis).
 *
 * Landing hub linking to all of MeepleAI's legal documents (Terms, Privacy,
 * Cookies, Takedown). Promotes the former redirect placeholder to a real hub.
 *
 * Mirrors the established legal-page pattern (see `terms/page.tsx`): shared
 * `LegalPageLayout`, i18n-driven `legal.hub.*` content, IT/EN toggle, and
 * JSON-LD structured data. The two linked sub-pages already exist.
 */

import { LegalPageLayout } from '@/components/legal';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Legal | MeepleAI',
  description:
    "MeepleAI's legal documents and policies — Terms of Service, Privacy Policy, Cookie Policy, and copyright takedown requests.",
  openGraph: {
    title: 'Legal | MeepleAI',
    description: "All of MeepleAI's legal documents and policies in one place.",
    url: 'https://meepleai.com/legal',
    type: 'website',
  },
  alternates: {
    canonical: 'https://meepleai.com/legal',
  },
};

const HUB_SECTIONS = ['documents'] as const;

export default function LegalHubPage() {
  return (
    <LegalPageLayout
      pageKey="hub"
      sections={HUB_SECTIONS}
      defaultOpenSection="documents"
      lastUpdated={new Date('2026-07-21')}
    />
  );
}
