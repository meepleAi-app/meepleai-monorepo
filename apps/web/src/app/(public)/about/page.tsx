/**
 * About Page
 *
 * Company information page with:
 * - Mission statement
 * - Our story
 * - Core values with icons
 * - Team section
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

// Value keys for the values section
const VALUE_KEYS = ['accessibility', 'precision', 'community', 'innovation'] as const;

// Icons for each value
const VALUE_ICONS: Record<(typeof VALUE_KEYS)[number], string> = {
  accessibility: '♿',
  precision: '🎯',
  community: '🤝',
  innovation: '💡',
};

export default function AboutPage() {
  const router = useRouter();
  const { t, locale } = useTranslation();

  return (
    <div className="min-h-dvh bg-background py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <h1
            className="text-3xl font-bold text-foreground"
            data-testid="about-heading"
          >
            {t('pages.about.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2 max-w-2xl mx-auto">
            {t('pages.about.description')}
          </p>
        </div>

        {/* Mission Section */}
        <Card className="mb-6">
          <CardHeader>
            <CardTitle className="text-xl flex items-center gap-2">
              <span>🎯</span>
              {t('pages.about.mission.title')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
              {t('pages.about.mission.content')}
            </p>
          </CardContent>
        </Card>

        {/* Story Section */}
        <Card className="mb-6">
          <CardHeader>
            <CardTitle className="text-xl flex items-center gap-2">
              <span>📖</span>
              {t('pages.about.story.title')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
              {t('pages.about.story.content')}
            </p>
          </CardContent>
        </Card>

        <Separator className="my-8" />

        {/* Values Section */}
        <div className="mb-8">
          <h2 className="text-2xl font-bold text-foreground text-center mb-6">
            {t('pages.about.values.title')}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {VALUE_KEYS.map((key) => (
              <Card key={key} className="bg-card">
                <CardHeader className="pb-2">
                  <CardTitle className="text-lg flex items-center gap-2">
                    {/* eslint-disable-next-line security/detect-object-injection */}
                    <span className="text-2xl">{VALUE_ICONS[key]}</span>
                    {t(`pages.about.values.${key}.title`)}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-slate-600 dark:text-slate-400">
                    {t(`pages.about.values.${key}.description`)}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>

        <Separator className="my-8" />

        {/* Team Section */}
        <Card className="mb-8">
          <CardHeader>
            <CardTitle className="text-xl flex items-center gap-2">
              <span>👥</span>
              {t('pages.about.team.title')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
              {t('pages.about.team.content')}
            </p>
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
