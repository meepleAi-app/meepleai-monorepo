/**
 * Privacy Policy Page
 *
 * GDPR-compliant privacy policy page with:
 * - Accordion-based sections for easy navigation
 * - Bilingual support (IT/EN) via i18n
 * - Responsive design following existing patterns
 * - Table of contents for quick navigation
 *
 * @see Issue for legal pages implementation
 */

'use client';

import { useRouter } from 'next/navigation';

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

// Section keys for privacy - order matters for display
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
] as const;

export default function PrivacyPage() {
  const router = useRouter();
  const { t, formatDate, locale } = useTranslation();

  // Last updated date - update when privacy policy changes
  const lastUpdated = new Date('2026-01-21');

  return (
    <div className="min-h-dvh bg-slate-50 dark:bg-slate-900 py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8">
          <h1
            className="text-3xl font-bold text-slate-900 dark:text-white"
            data-testid="privacy-heading"
          >
            {t('legal.privacy.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2">
            {t('legal.privacy.description')}
          </p>
          <p className="text-sm text-slate-500 dark:text-slate-500 mt-1">
            {t('legal.lastUpdated', {
              date: formatDate(lastUpdated, {
                year: 'numeric',
                month: 'long',
                day: 'numeric',
              }),
            })}
          </p>
        </div>

        {/* Table of Contents */}
        <Card className="mb-8">
          <CardHeader>
            <CardTitle className="text-lg">{t('legal.tableOfContents')}</CardTitle>
          </CardHeader>
          <CardContent>
            <nav>
              <ul className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                {PRIVACY_SECTIONS.map((sectionKey) => (
                  <li key={sectionKey}>
                    <a
                      href={`#${sectionKey}`}
                      className="text-blue-600 dark:text-blue-400 hover:underline"
                    >
                      {t(`legal.privacy.sections.${sectionKey}.title`)}
                    </a>
                  </li>
                ))}
              </ul>
            </nav>
          </CardContent>
        </Card>

        <Separator className="my-6" />

        {/* Privacy Sections */}
        <Accordion type="single" collapsible className="space-y-4" defaultValue="introduction">
          {PRIVACY_SECTIONS.map((sectionKey) => (
            <AccordionItem
              key={sectionKey}
              value={sectionKey}
              id={sectionKey}
              className="border rounded-lg px-4 bg-white dark:bg-slate-800"
            >
              <AccordionTrigger className="text-left font-semibold text-slate-900 dark:text-white hover:no-underline">
                {t(`legal.privacy.sections.${sectionKey}.title`)}
              </AccordionTrigger>
              <AccordionContent className="text-slate-600 dark:text-slate-300 leading-relaxed">
                {t(`legal.privacy.sections.${sectionKey}.content`)}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>

        {/* Footer Navigation */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← {locale === 'it' ? 'Torna alla Home' : 'Back to Home'}
          </Button>
          <Button variant="outline" onClick={() => router.push('/terms')}>
            {locale === 'it' ? 'Termini e Condizioni' : 'Terms and Conditions'} →
          </Button>
        </div>
      </div>
    </div>
  );
}
