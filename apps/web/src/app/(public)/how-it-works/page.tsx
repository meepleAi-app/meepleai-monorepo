// apps/web/src/app/(public)/how-it-works/page.tsx
'use client';

import Link from 'next/link';

import { Btn } from '@/components/ui/v2/btn';
import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

const STEPS = ['step1', 'step2', 'step3'] as const;
const STEP_EMOJI: Record<(typeof STEPS)[number], string> = {
  step1: '📚',
  step2: '💬',
  step3: '🎯',
};

const FEATURES = ['rag', 'multilingual', 'pdfUpload', 'gameLibrary'] as const;
const FEATURE_EMOJI: Record<(typeof FEATURES)[number], string> = {
  rag: '🔎',
  multilingual: '🌐',
  pdfUpload: '📄',
  gameLibrary: '🎲',
};

export default function HowItWorksPage() {
  const { t } = useTranslation();

  return (
    <main>
      <HeroGradient
        title={t('pages.howItWorks.title')}
        subtitle={t('pages.howItWorks.subtitle')}
        primaryCta={{ label: t('pages.howItWorks.ctaRegister'), href: '/register' }}
      />

      <section className="max-w-5xl mx-auto py-12 px-4">
        <h2 className="text-2xl font-bold text-center">{t('pages.howItWorks.stepsHeading')}</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mt-8">
          {STEPS.map((s, idx) => (
            <div key={s} className="text-center">
              <div className="text-5xl mb-3" aria-hidden="true">
                {STEP_EMOJI[s]}
              </div>
              <div className="text-sm font-mono text-muted-foreground mb-1">
                {String(idx + 1).padStart(2, '0')}
              </div>
              <h3 className="text-lg font-semibold mb-2">{t(`pages.howItWorks.${s}.title`)}</h3>
              <p className="text-muted-foreground">{t(`pages.howItWorks.${s}.description`)}</p>
            </div>
          ))}
        </div>
      </section>

      <Divider />

      <section className="max-w-5xl mx-auto py-12 px-4">
        <h2 className="text-2xl font-bold text-center">{t('pages.howItWorks.featuresHeading')}</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-8">
          {FEATURES.map(f => (
            <div key={f} className="p-4">
              <div className="text-4xl mb-2" aria-hidden="true">
                {FEATURE_EMOJI[f]}
              </div>
              <h3 className="text-lg font-semibold mb-1">
                {t(`pages.howItWorks.features.${f}.title`)}
              </h3>
              <p className="text-muted-foreground">
                {t(`pages.howItWorks.features.${f}.description`)}
              </p>
            </div>
          ))}
        </div>
      </section>

      <section className="max-w-5xl mx-auto px-4 py-8 flex flex-col sm:flex-row gap-3 justify-center items-center">
        <Btn variant="primary" asChild>
          <Link href="/about">{t('pages.howItWorks.aboutCta')}</Link>
        </Btn>
        <Btn variant="ghost" asChild>
          <Link href="/faq">{t('pages.howItWorks.faqCta')}</Link>
        </Btn>
      </section>
    </main>
  );
}
