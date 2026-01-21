/**
 * Blog Page (Placeholder)
 *
 * Blog placeholder page with:
 * - Coming soon message
 * - Category preview
 * - Social links
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

// Blog category keys
const CATEGORY_KEYS = ['news', 'guides', 'reviews', 'tutorials'] as const;

// Icons for each category
const CATEGORY_ICONS: Record<(typeof CATEGORY_KEYS)[number], string> = {
  news: '📰',
  guides: '📖',
  reviews: '⭐',
  tutorials: '🎓',
};

export default function BlogPage() {
  const router = useRouter();
  const { t, locale } = useTranslation();

  return (
    <div className="min-h-dvh bg-slate-50 dark:bg-slate-900 py-8 px-4">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="mb-8 text-center">
          <h1
            className="text-3xl font-bold text-slate-900 dark:text-white"
            data-testid="blog-heading"
          >
            {t('pages.blog.title')}
          </h1>
          <p className="text-slate-600 dark:text-slate-400 mt-2 max-w-2xl mx-auto">
            {t('pages.blog.description')}
          </p>
        </div>

        {/* Coming Soon Card */}
        <Card className="mb-8 bg-gradient-to-br from-blue-50 to-purple-50 dark:from-blue-900/20 dark:to-purple-900/20 border-blue-200 dark:border-blue-800">
          <CardContent className="pt-6 text-center py-12">
            <div className="text-6xl mb-4">🚀</div>
            <h2 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">
              {t('pages.blog.comingSoon.title')}
            </h2>
            <p className="text-slate-600 dark:text-slate-400 mb-6 max-w-md mx-auto">
              {t('pages.blog.comingSoon.content')}
            </p>
            <Button variant="outline">{t('pages.blog.comingSoon.cta')}</Button>
          </CardContent>
        </Card>

        <Separator className="my-8" />

        {/* Categories Preview */}
        <div className="mb-8">
          <h2 className="text-xl font-semibold text-slate-900 dark:text-white text-center mb-6">
            {locale === 'it' ? 'Categorie in arrivo' : 'Coming Categories'}
          </h2>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {CATEGORY_KEYS.map((key) => (
              <Card
                key={key}
                className="bg-white dark:bg-slate-800 opacity-60 hover:opacity-100 transition-opacity cursor-not-allowed"
              >
                <CardHeader className="pb-2 text-center">
                  <span className="text-3xl">{CATEGORY_ICONS[key]}</span>
                  <CardTitle className="text-sm mt-2">{t(`pages.blog.categories.${key}`)}</CardTitle>
                </CardHeader>
              </Card>
            ))}
          </div>
        </div>

        {/* Newsletter Teaser */}
        <Card className="bg-slate-100 dark:bg-slate-800">
          <CardContent className="pt-6 text-center">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-white mb-2">
              {locale === 'it' ? 'Resta aggiornato' : 'Stay Updated'}
            </h3>
            <p className="text-sm text-slate-600 dark:text-slate-400 mb-4">
              {locale === 'it'
                ? 'Seguici sui social per essere il primo a scoprire i nuovi contenuti.'
                : 'Follow us on social media to be the first to discover new content.'}
            </p>
            <div className="flex justify-center gap-4">
              <Button variant="ghost" size="sm">
                Twitter
              </Button>
              <Button variant="ghost" size="sm">
                Discord
              </Button>
              <Button variant="ghost" size="sm">
                GitHub
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Footer Navigation */}
        <div className="mt-12 flex flex-col sm:flex-row justify-between items-center gap-4 pt-8 border-t border-slate-200 dark:border-slate-700">
          <Button variant="ghost" onClick={() => router.push('/')}>
            ← {locale === 'it' ? 'Torna alla Home' : 'Back to Home'}
          </Button>
          <Button variant="outline" onClick={() => router.push('/faq')}>
            {locale === 'it' ? 'Domande Frequenti' : 'FAQ'} →
          </Button>
        </div>
      </div>
    </div>
  );
}
