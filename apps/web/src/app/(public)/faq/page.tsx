'use client';

/**
 * /faq — public FAQ page (V2, Wave A.1, Issue #583).
 *
 * Migrated from v1 (legacy Accordion + 4 hardcoded categories) to v2 with:
 *   - FAQSearchBar pill input with hash-driven query (?#q=...)
 *   - PopularGrid (top 4 popular FAQs) — only when query empty AND cat='all'
 *   - CategoryTabs sticky pills with live counts
 *   - AccordionItem v2 with markdown rendering + match highlighting
 *   - Empty / Loading / Error states (test-only `?loading=1` / `?error=1`)
 *
 * Mockup parity: admin-mockups/design_files/sp3-faq-enhanced.jsx
 * Spec: docs/superpowers/specs/2026-04-27-v2-migration-wave-a-1-faq.md
 */

import { type JSX, Suspense, useCallback, useMemo, useState } from 'react';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';

import { Btn } from '@/components/ui/v2/btn';
import { AccordionItem, CategoryTabs, FAQSearchBar, QuickAnswerCard } from '@/components/ui/v2/faq';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useFaqHashQuery } from '@/hooks/useFaqHashQuery';
import { useTranslation } from '@/hooks/useTranslation';
import { type CategoryId, type FAQ, FAQ_CATEGORIES, FAQS, POPULAR_FAQS } from '@/lib/faq/data';
import { countByCategory, filterFAQs, highlight, renderInline, renderLong } from '@/lib/faq/search';

const IS_NON_PROD = process.env.NODE_ENV !== 'production';

/**
 * Outer page wraps the body in Suspense — required by Next.js App Router
 * when a client component calls `useSearchParams()` (CSR bailout). The
 * fallback re-uses the hero so the layout stays stable while the search
 * params hydrate on the client.
 */
export default function FaqPage(): JSX.Element {
  const { t } = useTranslation();
  return (
    <Suspense
      fallback={
        <main>
          <div data-testid="faq-hero">
            <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />
          </div>
        </main>
      }
    >
      <FaqPageBody />
    </Suspense>
  );
}

function FaqPageBody(): JSX.Element {
  const { t } = useTranslation();
  const searchParams = useSearchParams();

  const { query, setQuery } = useFaqHashQuery();
  const [activeCat, setActiveCat] = useState<CategoryId>('all');
  const [openIds, setOpenIds] = useState<ReadonlySet<string>>(() => new Set());

  const resolve = useCallback(
    (faq: FAQ) => ({
      q: t(faq.questionKey),
      short: t(faq.shortKey),
      long: t(faq.longKey),
    }),
    [t]
  );

  const resolveCategoryLabel = useCallback(
    (catId: Exclude<CategoryId, 'all'>) => t(`pages.faq.categories.${catId}.label`),
    [t]
  );

  const filteredFaqs = useMemo(
    () => filterFAQs(FAQS, { activeCat, query, resolve }),
    [activeCat, query, resolve]
  );

  const counts = useMemo(
    () => countByCategory(FAQS, query, FAQ_CATEGORIES, resolve),
    [query, resolve]
  );

  const toggleOpen = useCallback((id: string) => {
    setOpenIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const handleQuickAnswerClick = useCallback(
    (faq: FAQ) => {
      setQuery('');
      setActiveCat(faq.cat);
      setOpenIds(new Set([faq.id]));
    },
    [setQuery]
  );

  // Test-only state injection — guarded behind NODE_ENV !== 'production'
  // so the URL-driven branches are stripped from the production build entirely
  // (constant folding via `IS_NON_PROD`).
  const isLoading = IS_NON_PROD && searchParams?.get('loading') === '1';
  const hasError = IS_NON_PROD && searchParams?.get('error') === '1';

  if (isLoading) {
    return (
      <main>
        <div data-testid="faq-hero">
          <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />
        </div>
        <section
          aria-busy="true"
          aria-live="polite"
          aria-label={t('pages.faq.loadingState.label')}
          className="mx-auto max-w-[880px] px-4 py-8 sm:px-8"
        >
          <span className="sr-only">{t('pages.faq.loadingState.label')}</span>
          <div className="mb-6 grid gap-3 sm:grid-cols-2">
            {[0, 1, 2, 3].map(i => (
              <div
                key={i}
                className="h-[130px] rounded-lg bg-[hsl(var(--bg-muted))] mai-shimmer motion-reduce:animate-none"
              />
            ))}
          </div>
          <div className="mb-4 flex gap-2">
            {[0, 1, 2, 3, 4].map(i => (
              <div
                key={i}
                className="h-8 w-[100px] rounded-full bg-[hsl(var(--bg-muted))] mai-shimmer motion-reduce:animate-none"
              />
            ))}
          </div>
          {[0, 1, 2, 3, 4].map(i => (
            <div
              key={i}
              className="mb-px h-[60px] bg-[hsl(var(--bg-muted))] mai-shimmer motion-reduce:animate-none"
            />
          ))}
        </section>
      </main>
    );
  }

  if (hasError) {
    return (
      <main>
        <div data-testid="faq-hero">
          <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />
        </div>
        <section className="mx-auto max-w-[720px] px-4 py-12 sm:px-8">
          <div
            role="alert"
            className="flex flex-col items-start gap-3 rounded-lg border border-[hsl(var(--c-danger)/0.25)] bg-[hsl(var(--c-danger)/0.1)] p-5"
          >
            <h2 className="m-0 font-display text-lg font-bold text-[hsl(var(--c-danger))]">
              {t('pages.faq.errorState.title')}
            </h2>
            <p className="m-0 text-sm text-[hsl(var(--text-sec))]">
              {t('pages.faq.errorState.subtitle')}
            </p>
            <Btn variant="primary" asChild>
              <Link href="/faq">{t('pages.faq.errorState.retryCta')}</Link>
            </Btn>
          </div>
        </section>
      </main>
    );
  }

  const trimmedQuery = query.trim();
  const showPopularGrid = trimmedQuery === '' && activeCat === 'all';
  const showEmptyState = trimmedQuery !== '' && filteredFaqs.length === 0;

  return (
    <main>
      <div data-testid="faq-hero">
        <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />
      </div>

      {/* Search bar */}
      <section className="mx-auto -mt-6 max-w-[580px] px-4 sm:px-8">
        <FAQSearchBar
          value={query}
          onChange={setQuery}
          placeholder={t('pages.faq.search.placeholder')}
          ariaLabel={t('pages.faq.search.label')}
          clearLabel={t('pages.faq.search.clearLabel')}
        />
      </section>

      <div className="mx-auto max-w-[880px] px-4 pb-14 pt-7 sm:px-8">
        {/* Search status banner — only when query active and we have results */}
        {trimmedQuery !== '' && filteredFaqs.length > 0 && (
          <div
            role="status"
            data-dynamic="search-status"
            className="mb-4 flex items-start gap-2.5 rounded-md border border-[hsl(var(--c-success)/0.25)] bg-[hsl(var(--c-success)/0.1)] px-3.5 py-3 text-sm font-semibold text-[hsl(var(--c-success))]"
          >
            <span aria-hidden="true">✓</span>
            <span className="flex-1 text-foreground">
              {t('pages.faq.search.resultsLabel', {
                count: filteredFaqs.length,
                query: trimmedQuery,
              })}
            </span>
          </div>
        )}

        {/* Empty state (search yielded zero) */}
        {showEmptyState && (
          <div
            role="status"
            className="mb-6 flex flex-col items-start gap-3 rounded-md border border-[hsl(var(--c-info)/0.25)] bg-[hsl(var(--c-info)/0.1)] px-3.5 py-4"
          >
            <h2 className="m-0 font-display text-base font-bold text-foreground">
              {t('pages.faq.emptyState.title')}
            </h2>
            <p className="m-0 text-sm text-[hsl(var(--text-sec))]">
              {t('pages.faq.emptyState.subtitle')}
            </p>
            <Btn variant="primary" asChild>
              <Link href="/contact">{t('pages.faq.emptyState.contactCta')}</Link>
            </Btn>
          </div>
        )}

        {/* Popular grid (top 4) */}
        {showPopularGrid && (
          <section className="mb-6">
            <header className="mb-3 flex items-baseline gap-2">
              <h2 className="m-0 font-display text-sm font-bold uppercase tracking-[0.08em] text-foreground">
                {t('pages.faq.popularSection.title')}
              </h2>
              <span className="font-mono text-[11px] font-semibold text-[hsl(var(--text-muted))]">
                {t('pages.faq.popularSection.subtitle')}
              </span>
            </header>
            <div className="grid gap-3 sm:grid-cols-2">
              {POPULAR_FAQS.map(faq => {
                const cat = FAQ_CATEGORIES.find(c => c.id === faq.cat);
                if (!cat) return null;
                const r = resolve(faq);
                return (
                  <QuickAnswerCard
                    key={faq.id}
                    question={r.q}
                    short={r.short}
                    categoryLabel={resolveCategoryLabel(faq.cat)}
                    categoryIcon={cat.icon}
                    popularRank={faq.popularRank ?? 0}
                    onClick={() => handleQuickAnswerClick(faq)}
                  />
                );
              })}
            </div>
          </section>
        )}

        {/* Category tabs */}
        {!showEmptyState && (
          <div className="mb-2">
            <CategoryTabs
              categories={FAQ_CATEGORIES}
              active={activeCat}
              onChange={setActiveCat}
              counts={counts}
              resolveLabel={cat =>
                cat.id === 'all'
                  ? t('pages.faq.categories.all.label')
                  : resolveCategoryLabel(cat.id)
              }
              ariaLabel={t('pages.faq.search.label')}
            />
          </div>
        )}

        {/* Accordion list */}
        {!showEmptyState && filteredFaqs.length > 0 && (
          <div
            data-dynamic="faq-list"
            className="mt-1 rounded-lg border border-border bg-card px-4"
          >
            {filteredFaqs.map(faq => {
              const cat = FAQ_CATEGORIES.find(c => c.id === faq.cat);
              if (!cat) return null;
              const r = resolve(faq);
              const isOpen = openIds.has(faq.id);
              return (
                <AccordionItem
                  key={faq.id}
                  id={faq.id}
                  question={highlight(r.q, query)}
                  answer={isOpen ? renderLong(r.long, query) : renderInline(r.short, query)}
                  categoryLabel={resolveCategoryLabel(faq.cat)}
                  categoryIcon={cat.icon}
                  isOpen={isOpen}
                  onToggle={() => toggleOpen(faq.id)}
                />
              );
            })}
          </div>
        )}

        {/* Footer CTA */}
        <section className="mt-8">
          <div className="flex flex-col items-center gap-2.5 rounded-xl border border-[hsl(var(--c-game)/0.2)] bg-gradient-to-br from-[hsl(var(--c-game)/0.08)] to-[hsl(var(--c-event)/0.06)] px-6 py-7 text-center">
            <div
              aria-hidden="true"
              className="mb-1 flex h-14 w-14 items-center justify-center rounded-full bg-[hsl(var(--c-game)/0.15)] text-[26px]"
            >
              💬
            </div>
            <h3 className="m-0 font-display text-lg font-bold text-foreground">
              {t('pages.faq.footer.title')}
            </h3>
            <p className="m-0 mb-1 max-w-[420px] text-[13.5px] leading-[1.6] text-[hsl(var(--text-sec))]">
              {t('pages.faq.footer.subtitle')}
            </p>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Btn variant="primary" asChild>
                <Link href="/contact">{t('pages.faq.contactCta')}</Link>
              </Btn>
              <Btn variant="ghost" asChild>
                <Link href="/how-it-works">{t('pages.faq.howItWorksCta')}</Link>
              </Btn>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
