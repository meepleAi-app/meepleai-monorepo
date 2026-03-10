/**
 * LegalPageLayout - Shared layout for all legal pages
 *
 * Provides consistent structure for Privacy, Terms, and Cookie pages:
 * - Language toggle (IT/EN)
 * - Table of contents
 * - Accordion-based sections with rich markdown content
 * - Footer navigation between legal pages
 * - JSON-LD structured data
 * - Open Graph meta tags
 *
 * Step 10 of the GDPR integration plan.
 */

'use client';

import Link from 'next/link';

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

import { LegalLocaleProvider, LegalLocaleToggle } from './LegalLocaleToggle';
import { LegalMarkdown } from './LegalMarkdown';
import { StructuredData, legalPageSchema, breadcrumbSchema } from './StructuredData';

type LegalPageKey = 'privacy' | 'terms' | 'cookies';

interface FooterNavLink {
  href: string;
  labelIt: string;
  labelEn: string;
}

interface LegalPageLayoutProps {
  /** Which legal page this is */
  pageKey: LegalPageKey;
  /** Ordered section keys for the accordion */
  sections: readonly string[];
  /** Which section to open by default */
  defaultOpenSection?: string;
  /** Last updated date */
  lastUpdated: Date;
  /** Navigation link for the left footer button */
  prevLink?: FooterNavLink;
  /** Navigation link for the right footer button */
  nextLink?: FooterNavLink;
  /** Meta description for SEO */
  metaDescription?: string;
}

const BASE_URL = process.env.NEXT_PUBLIC_BASE_URL || 'https://meepleai.com';

const PAGE_PATHS: Record<LegalPageKey, string> = {
  privacy: '/privacy',
  terms: '/terms',
  cookies: '/cookies',
};

function LegalPageContent({
  pageKey,
  sections,
  defaultOpenSection,
  lastUpdated,
  prevLink,
  nextLink,
}: LegalPageLayoutProps) {
  const { t, formatDate, locale } = useTranslation();

  const title = t(`legal.${pageKey}.title`);
  const description = t(`legal.${pageKey}.description`);

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* JSON-LD Structured Data */}
        <StructuredData
          data={legalPageSchema({
            name: title,
            description,
            url: `${BASE_URL}${PAGE_PATHS[pageKey]}`,
            lastUpdated: lastUpdated.toISOString().split('T')[0],
          })}
        />
        <StructuredData
          data={breadcrumbSchema([
            { name: 'Home', url: BASE_URL },
            { name: title, url: `${BASE_URL}${PAGE_PATHS[pageKey]}` },
          ])}
        />

        {/* Header with Language Toggle */}
        <div className="mb-8">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-3xl font-bold text-foreground" data-testid={`${pageKey}-heading`}>
                {title}
              </h1>
              <p className="text-slate-600 dark:text-slate-400 mt-2">{description}</p>
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
            <LegalLocaleToggle className="flex-shrink-0 mt-1" />
          </div>
        </div>

        {/* Table of Contents */}
        <Card className="mb-8">
          <CardHeader>
            <CardTitle className="text-lg">{t('legal.tableOfContents')}</CardTitle>
          </CardHeader>
          <CardContent>
            <nav aria-label={locale === 'it' ? 'Indice della pagina' : 'Page index'}>
              <ul className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                {sections.map(sectionKey => (
                  <li key={sectionKey}>
                    <a
                      href={`#${sectionKey}`}
                      className="text-blue-600 dark:text-blue-400 hover:underline"
                    >
                      {t(`legal.${pageKey}.sections.${sectionKey}.title`)}
                    </a>
                  </li>
                ))}
              </ul>
            </nav>
          </CardContent>
        </Card>

        <Separator className="my-6" />

        {/* Sections */}
        <Accordion
          type="single"
          collapsible
          className="space-y-4"
          defaultValue={defaultOpenSection || sections[0]}
        >
          {sections.map(sectionKey => (
            <AccordionItem
              key={sectionKey}
              value={sectionKey}
              id={sectionKey}
              className="border rounded-lg px-4 bg-card"
            >
              <AccordionTrigger className="text-left font-semibold text-foreground hover:no-underline">
                {t(`legal.${pageKey}.sections.${sectionKey}.title`)}
              </AccordionTrigger>
              <AccordionContent>
                <LegalMarkdown content={t(`legal.${pageKey}.sections.${sectionKey}.content`)} />
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>

        {/* Footer Navigation — uses <Link> for SEO, accessibility, and right-click support */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          {prevLink ? (
            <Button variant="ghost" asChild>
              <Link href={prevLink.href}>
                &larr; {locale === 'it' ? prevLink.labelIt : prevLink.labelEn}
              </Link>
            </Button>
          ) : (
            <Button variant="ghost" asChild>
              <Link href="/">&larr; {locale === 'it' ? 'Torna alla Home' : 'Back to Home'}</Link>
            </Button>
          )}
          {nextLink && (
            <Button variant="outline" asChild>
              <Link href={nextLink.href}>
                {locale === 'it' ? nextLink.labelIt : nextLink.labelEn} &rarr;
              </Link>
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

/**
 * LegalPageLayout wraps content with LegalLocaleProvider
 * so the language toggle works independently of app locale.
 */
export function LegalPageLayout(props: LegalPageLayoutProps) {
  return (
    <LegalLocaleProvider>
      <LegalPageContent {...props} />
    </LegalLocaleProvider>
  );
}
