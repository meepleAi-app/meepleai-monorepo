/**
 * FAQ Page
 *
 * Frequently asked questions page with:
 * - Categorized questions with accordion
 * - Search functionality (future)
 * - Bilingual support (IT/EN) via i18n
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

// FAQ category keys
const CATEGORY_KEYS = ['general', 'usage', 'technical', 'account'] as const;

// Questions per category (must match i18n keys q1/q2/q3)
const QUESTIONS_PER_CATEGORY = ['q1', 'q2', 'q3'] as const;

// Icons for each category
const CATEGORY_ICONS: Record<(typeof CATEGORY_KEYS)[number], string> = {
  general: '📋',
  usage: '🎮',
  technical: '⚙️',
  account: '👤',
};

export default function FaqPage() {
  const router = useRouter();
  const { t, locale } = useTranslation();

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-foreground" data-testid="faq-heading">
            {t('pages.faq.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2 max-w-2xl mx-auto">
            {t('pages.faq.description')}
          </p>
        </div>

        {/* FAQ Categories */}
        <div className="space-y-8">
          {CATEGORY_KEYS.map(categoryKey => (
            <Card key={categoryKey} className="overflow-hidden">
              <CardHeader className="bg-muted dark:bg-card">
                <CardTitle className="text-lg flex items-center gap-2">
                  {/* eslint-disable-next-line security/detect-object-injection */}
                  <span className="text-xl">{CATEGORY_ICONS[categoryKey]}</span>
                  {t(`pages.faq.categories.${categoryKey}.title`)}
                </CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <Accordion type="single" collapsible className="w-full">
                  {QUESTIONS_PER_CATEGORY.map(questionKey => (
                    <AccordionItem
                      key={`${categoryKey}-${questionKey}`}
                      value={`${categoryKey}-${questionKey}`}
                      className="border-b last:border-b-0 px-6"
                    >
                      <AccordionTrigger className="text-left font-medium text-foreground hover:no-underline py-4">
                        {t(`pages.faq.categories.${categoryKey}.questions.${questionKey}.question`)}
                      </AccordionTrigger>
                      <AccordionContent className="text-slate-600 dark:text-slate-300 pb-4">
                        {t(`pages.faq.categories.${categoryKey}.questions.${questionKey}.answer`)}
                      </AccordionContent>
                    </AccordionItem>
                  ))}
                </Accordion>
              </CardContent>
            </Card>
          ))}
        </div>

        <Separator className="my-8" />

        {/* Still have questions? */}
        <Card className="bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800">
          <CardContent className="pt-6 text-center">
            <h3 className="text-xl font-semibold text-foreground mb-2">
              {locale === 'it' ? 'Non hai trovato la risposta?' : "Didn't find your answer?"}
            </h3>
            <p className="text-slate-600 dark:text-slate-400 mb-4">
              {locale === 'it'
                ? 'Contattaci direttamente, siamo qui per aiutarti.'
                : "Contact us directly, we're here to help."}
            </p>
            <Button onClick={() => router.push('/contact')}>
              {locale === 'it' ? 'Contattaci' : 'Contact Us'}
            </Button>
          </CardContent>
        </Card>

        {/* Footer Navigation */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← {locale === 'it' ? 'Torna alla Home' : 'Back to Home'}
          </Button>
          <Button variant="outline" onClick={() => router.push('/how-it-works')}>
            {locale === 'it' ? 'Come Funziona' : 'How It Works'} →
          </Button>
        </div>
      </div>
    </div>
  );
}
