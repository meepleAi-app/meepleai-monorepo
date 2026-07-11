/**
 * Takedown Request Page — /legal/takedown (ME-M1.7, Issue #529).
 *
 * Public (NOT login-gated) policy page describing how to file a copyright
 * takedown request for a published mechanic card, plus a functional template
 * form that composes a pre-filled email to `takedown@meepleai.app` (ADR-051 §
 * "Follow-up obbligatori"). No backend intake endpoint exists — the form is a
 * client-side mailto/copy template, mounted in LegalPageLayout's `footerSlot`.
 *
 * Mirrors the established legal-page pattern (see `terms/page.tsx`): shared
 * `LegalPageLayout`, i18n-driven `legal.takedown.*` content, IT/EN toggle, and
 * JSON-LD structured data.
 */

import { LegalPageLayout, TakedownRequestForm } from '@/components/legal';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Takedown Request | MeepleAI',
  description:
    'Report content on MeepleAI that you believe infringes your copyright. Includes a template request form and our response commitments.',
  openGraph: {
    title: 'Takedown Request | MeepleAI',
    description:
      'Report content on MeepleAI that you believe infringes your copyright, and learn how we handle takedown requests.',
    url: 'https://meepleai.com/legal/takedown',
    type: 'website',
  },
  alternates: {
    canonical: 'https://meepleai.com/legal/takedown',
  },
};

const TAKEDOWN_SECTIONS = [
  'overview',
  'whoCanFile',
  'whatToInclude',
  'howWeRespond',
  'contact',
] as const;

export default function TakedownPage() {
  return (
    <LegalPageLayout
      pageKey="takedown"
      sections={TAKEDOWN_SECTIONS}
      defaultOpenSection="overview"
      lastUpdated={new Date('2026-07-11')}
      prevLink={{
        href: '/terms',
        labelIt: 'Termini e Condizioni',
        labelEn: 'Terms and Conditions',
      }}
      footerSlot={<TakedownRequestForm />}
    />
  );
}
