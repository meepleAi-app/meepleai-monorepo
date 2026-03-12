/**
 * Landing Page (Marketing) - App Router Server Component
 *
 * Public-facing marketing page with hero, how-it-works, social proof, and CTA.
 * Fully server-rendered for optimal SEO and performance.
 *
 * SEO Features:
 * - Server-side rendering (SSR)
 * - Structured data (JSON-LD)
 * - Open Graph metadata
 * - Twitter Card metadata
 * - Semantic HTML5
 */

import { redirect } from 'next/navigation';

import { HowItWorksSteps, SocialProofBar, WelcomeCTA, WelcomeHero } from '@/components/landing';
import { getServerUser } from '@/lib/auth/server';

import type { Metadata } from 'next';

// Metadata for SEO
export const metadata: Metadata = {
  title: 'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo',
  description:
    'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo. Gratis per iniziare.',
  keywords: [
    'giochi da tavolo',
    'regole',
    'AI',
    'arbitro',
    'agente AI',
    'italiano',
    'board games',
    'meeple',
    'intelligenza artificiale',
    'serata giochi',
  ],
  authors: [{ name: 'MeepleAI Team' }],
  creator: 'MeepleAI',
  publisher: 'MeepleAI',
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      'max-video-preview': -1,
      'max-image-preview': 'large',
      'max-snippet': -1,
    },
  },
  openGraph: {
    type: 'website',
    locale: 'it_IT',
    url: 'https://meepleai.dev',
    siteName: 'MeepleAI',
    title: 'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo',
    description:
      'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo.',
    images: [
      {
        url: '/og-image.png',
        width: 1200,
        height: 630,
        alt: 'MeepleAI — Arbitro AI per giochi da tavolo',
      },
    ],
  },
  twitter: {
    card: 'summary_large_image',
    site: '@MeepleAI',
    creator: '@MeepleAI',
    title: 'MeepleAI — Il tuo arbitro AI per le serate giochi da tavolo',
    description:
      'Un agente AI che conosce le regole del tuo gioco. Setup, punteggi, dispute — ti assiste al tavolo.',
    images: ['/twitter-card.png'],
  },
  alternates: {
    canonical: 'https://meepleai.dev',
    languages: {
      'it-IT': 'https://meepleai.dev',
    },
  },
  category: 'technology',
};

// Structured data constant (safe - no user input)
const structuredData = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: 'MeepleAI',
  applicationCategory: 'GameApplication',
  applicationSubCategory: 'Board Game Assistant',
  operatingSystem: 'Web, iOS, Android',
  offers: {
    '@type': 'Offer',
    price: '0',
    priceCurrency: 'EUR',
  },
  description:
    'AI-powered board game companion that assists at the table with setup, rules, scoring, and disputes.',
  inLanguage: 'it-IT',
  featureList: [
    'Arbitro AI al tavolo',
    'Setup e regole automatiche',
    'Punteggi e dispute',
    'Salvataggio partita',
  ],
};

/**
 * Landing Page - Server Component
 *
 * Renders marketing landing page with hero, how-it-works, social proof, and CTA.
 * All sections are server-rendered for optimal performance and SEO.
 */
export default async function LandingPage() {
  // Server-side redirect: authenticated users go straight to dashboard (no flash)
  const user = await getServerUser();
  if (user) {
    redirect('/dashboard');
  }

  return (
    <>
      {/* Structured Data for SEO - Static content only, no user input */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(structuredData),
        }}
      />

      {/* Header provided by (public)/layout.tsx → PublicLayout → PublicHeader */}

      {/* Main Landing Content — <main> wrapper provided by PublicLayout */}
      <WelcomeHero />
      <HowItWorksSteps />
      <SocialProofBar />
      <WelcomeCTA />
    </>
  );
}
