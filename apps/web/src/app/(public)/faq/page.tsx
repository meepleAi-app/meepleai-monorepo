'use client';

import { Fragment } from 'react';

import Link from 'next/link';

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Btn } from '@/components/ui/v2/btn';
import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

const CATEGORIES = ['general', 'usage', 'technical', 'account'] as const;
const QUESTIONS = ['q1', 'q2', 'q3'] as const;
const CATEGORY_EMOJI: Record<(typeof CATEGORIES)[number], string> = {
  general: '💡',
  usage: '🎲',
  technical: '⚙️',
  account: '👤',
};

export default function FaqPage() {
  const { t } = useTranslation();

  return (
    <main>
      <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />

      {CATEGORIES.map((cat, idx) => (
        <Fragment key={cat}>
          {idx > 0 && <Divider />}
          <section className="max-w-3xl mx-auto py-8 px-4">
            <h2 className="text-2xl font-bold mb-6">
              <span aria-hidden="true" className="mr-2">
                {CATEGORY_EMOJI[cat]}
              </span>
              {t(`pages.faq.categories.${cat}`)}
            </h2>
            <Accordion type="single" collapsible>
              {QUESTIONS.map(q => (
                <AccordionItem key={q} value={`${cat}-${q}`}>
                  <AccordionTrigger>{t(`pages.faq.items.${cat}.${q}.question`)}</AccordionTrigger>
                  <AccordionContent>{t(`pages.faq.items.${cat}.${q}.answer`)}</AccordionContent>
                </AccordionItem>
              ))}
            </Accordion>
          </section>
        </Fragment>
      ))}

      <section className="max-w-3xl mx-auto py-12 flex flex-col sm:flex-row gap-3 justify-center items-center">
        <Btn variant="primary" asChild>
          <Link href="/contact">{t('pages.faq.contactCta')}</Link>
        </Btn>
        <Btn variant="ghost" asChild>
          <Link href="/how-it-works">{t('pages.faq.howItWorksCta')}</Link>
        </Btn>
      </section>
    </main>
  );
}
