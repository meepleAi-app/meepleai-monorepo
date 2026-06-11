'use client';

import Link from 'next/link';

import { HeroGradient } from '@/components/ui/hero-gradient/hero-gradient';
import { cn } from '@/lib/utils';

import { CommunityStatsRow, type CommunityStats } from './CommunityStatsRow';
import { FeaturedGamesCarousel, type FeaturedGame } from './FeaturedGamesCarousel';

interface LibraryPublicHomeProps {
  featured: FeaturedGame[];
  stats: CommunityStats;
}

/**
 * Public landing page for /library-public — community-facing showcase.
 *
 * #2208 DS-17-10 sub-issue (forward-refactor design_intent 0.6 conf per memory):
 * - HeroGradient primitive (primary CTA /join + secondary CTA /how-it-works)
 * - CommunityStatsRow (4-column grid)
 * - FeaturedGamesCarousel (4-6 MeepleCard grid horizontal scroll)
 * - WhatYouCanDo section (3 bullets)
 * - CTA strip "Crea il tuo account"
 *
 * Mockup parity: `admin-mockups/design_files/sp3-library-public.jsx` (816 LOC, 25 components — simplified scaffold here, full forward-refactor verification deferred to designer review tracking issue per DEC-4).
 */
export function LibraryPublicHome({ featured, stats }: LibraryPublicHomeProps) {
  return (
    <main className="flex flex-col gap-12 pb-12">
      {/* HERO via reusable primitive */}
      <HeroGradient
        title="Scopri la community board game di MeepleAI"
        subtitle="Migliaia di giochi catalogati, regole spiegate dall'AI, partite condivise, contenuti dalla community. Tutto in un posto."
        primaryCta={{ label: 'Inizia gratis', href: '/join' }}
        secondaryCta={{ label: 'Come funziona', href: '/how-it-works' }}
      />

      <div className="flex flex-col gap-12 px-4 sm:px-8 lg:px-16">
        {/* STATS */}
        <section aria-labelledby="library-public-stats-title">
          <h2 id="library-public-stats-title" className="sr-only">
            Statistiche community
          </h2>
          <CommunityStatsRow stats={stats} />
        </section>

        {/* FEATURED */}
        <section aria-labelledby="library-public-featured-title" className="flex flex-col gap-4">
          <h2
            id="library-public-featured-title"
            className="font-quicksand text-2xl font-semibold tracking-tight text-foreground sm:text-3xl"
          >
            Giochi in evidenza
          </h2>
          <FeaturedGamesCarousel games={featured} />
        </section>

        {/* WHAT YOU CAN DO (3 bullets) */}
        <section
          aria-labelledby="library-public-features-title"
          className="grid grid-cols-1 gap-4 sm:grid-cols-3"
        >
          <h2 id="library-public-features-title" className="sr-only">
            Cosa puoi fare con MeepleAI
          </h2>
          {[
            {
              title: 'Chiedi le regole',
              body: 'AI esperti rispondono a qualsiasi dubbio sul regolamento.',
            },
            {
              title: 'Organizza partite',
              body: 'Crea serate, invita amici, traccia punteggi automaticamente.',
            },
            {
              title: 'Condividi contenuti',
              body: 'Toolkit, agenti AI, guide pubblicabili per la community.',
            },
          ].map(item => (
            <article
              key={item.title}
              className={cn(
                'flex flex-col gap-2 rounded-2xl border border-border/50 bg-card/90 p-6 backdrop-blur-md',
                'hover:translate-y-0 hover:shadow-sm dark:hover:shadow-sm'
              )}
            >
              <h3 className="font-quicksand text-lg font-semibold text-foreground">{item.title}</h3>
              <p className="text-sm leading-relaxed text-muted-foreground">{item.body}</p>
            </article>
          ))}
        </section>

        {/* CTA FOOTER */}
        <section
          className="flex flex-col items-center gap-4 rounded-2xl border border-border/50 bg-card/90 px-6 py-12 text-center backdrop-blur-md"
          aria-labelledby="library-public-cta-title"
        >
          <h2
            id="library-public-cta-title"
            className="font-quicksand text-3xl font-bold text-foreground sm:text-4xl"
          >
            Pronto a giocare con noi?
          </h2>
          <p className="max-w-2xl text-base text-muted-foreground">
            Crea il tuo account e accedi al catalogo, agli agenti AI, ai toolkit della community.
          </p>
          <Link
            href="/join"
            className="inline-flex items-center justify-center rounded-md bg-primary px-6 py-3 text-base font-semibold text-primary-foreground transition-colors hover:bg-primary/90"
          >
            Crea account gratis
          </Link>
        </section>
      </div>
    </main>
  );
}
