/**
 * How It Works Page
 *
 * Explains how MeepleAI works with:
 * - Step-by-step guide
 * - Feature highlights
 * - Visual flow explanation
 * - Bilingual support (IT/EN) via i18n
 *
 * @see Issue for legal pages implementation
 */

'use client';

import { useRouter } from 'next/navigation';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

// Step keys for the steps section
const STEP_KEYS = ['step1', 'step2', 'step3'] as const;

// Feature keys for the features section
const FEATURE_KEYS = ['rag', 'multilingual', 'pdfUpload', 'gameLibrary'] as const;

// Icons for each step
const STEP_ICONS: Record<(typeof STEP_KEYS)[number], string> = {
  step1: '🎮',
  step2: '❓',
  step3: '✅',
};

// Icons for each feature
const FEATURE_ICONS: Record<(typeof FEATURE_KEYS)[number], string> = {
  rag: '🧠',
  multilingual: '🌍',
  pdfUpload: '📄',
  gameLibrary: '📚',
};

export default function HowItWorksPage() {
  const router = useRouter();
  const { t, locale } = useTranslation();

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <h1
            className="text-3xl font-bold text-foreground"
            data-testid="how-it-works-heading"
          >
            {t('pages.howItWorks.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2 max-w-2xl mx-auto">
            {t('pages.howItWorks.description')}
          </p>
        </div>

        {/* Steps Section */}
        <div className="mb-12">
          <div className="space-y-6">
            {STEP_KEYS.map((key, index) => (
              <div key={key} className="flex gap-4 items-start">
                <div className="flex-shrink-0 w-16 h-16 bg-blue-100 dark:bg-blue-900 rounded-full flex items-center justify-center text-3xl">
                  {/* eslint-disable-next-line security/detect-object-injection */}
                  {STEP_ICONS[key]}
                </div>
                <Card className="flex-grow">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-lg">
                      {t(`pages.howItWorks.steps.${key}.title`)}
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <p className="text-slate-600 dark:text-slate-400">
                      {t(`pages.howItWorks.steps.${key}.description`)}
                    </p>
                  </CardContent>
                </Card>
                {index < STEP_KEYS.length - 1 && (
                  <div className="hidden md:block absolute left-8 mt-16 h-8 w-0.5 bg-blue-200 dark:bg-blue-800" />
                )}
              </div>
            ))}
          </div>
        </div>

        <Separator className="my-8" />

        {/* Features Section */}
        <div className="mb-8">
          <h2 className="text-2xl font-bold text-foreground text-center mb-6">
            {t('pages.howItWorks.features.title')}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {FEATURE_KEYS.map((key) => (
              <Card key={key} className="bg-card">
                <CardHeader className="pb-2">
                  <CardTitle className="text-lg flex items-center gap-2">
                    {/* eslint-disable-next-line security/detect-object-injection */}
                    <span className="text-2xl">{FEATURE_ICONS[key]}</span>
                    {t(`pages.howItWorks.features.${key}.title`)}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-slate-600 dark:text-slate-400">
                    {t(`pages.howItWorks.features.${key}.description`)}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>

        {/* CTA Section */}
        <Card className="bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800">
          <CardContent className="pt-6 text-center">
            <h3 className="text-xl font-semibold text-foreground mb-2">
              {locale === 'it' ? 'Pronto a iniziare?' : 'Ready to get started?'}
            </h3>
            <p className="text-slate-600 dark:text-slate-400 mb-4">
              {locale === 'it'
                ? 'Prova MeepleAI gratuitamente e scopri quanto può essere semplice capire le regole.'
                : 'Try MeepleAI for free and discover how easy understanding rules can be.'}
            </p>
            <Button onClick={() => router.push('/register')}>
              {locale === 'it' ? 'Inizia Ora' : 'Start Now'}
            </Button>
          </CardContent>
        </Card>

        {/* Footer Navigation */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          <Button variant="ghost" onClick={() => router.push('/about')}>
            ← {locale === 'it' ? 'Chi Siamo' : 'About Us'}
          </Button>
          <Button variant="outline" onClick={() => router.push('/faq')}>
            {locale === 'it' ? 'Domande Frequenti' : 'FAQ'} →
          </Button>
        </div>
      </div>
    </div>
  );
}
