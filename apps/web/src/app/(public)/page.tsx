/**
 * Landing Page (Marketing) - App Router Server Component
 *
 * Public-facing marketing page with hero, features, and how-it-works sections.
 * Fully server-rendered for optimal SEO and performance.
 *
 * Issue #1835: PAGE-001 - Landing Page (Marketing)
 * Design: Playful Boardroom (wireframes-playful-boardroom.md)
 *
 * Performance Targets:
 * - Lighthouse Performance: ≥90
 * - Lighthouse Accessibility: ≥95
 * - First Contentful Paint: <1.5s
 * - Time to Interactive: <2.5s
 *
 * SEO Features:
 * - Server-side rendering (SSR)
 * - Structured data (JSON-LD)
 * - Open Graph metadata
 * - Twitter Card metadata
 * - Semantic HTML5
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import { redirect } from 'next/navigation';

import { CallToActionSection } from '@/components/landing/CallToActionSection';
import { FeaturesSection } from '@/components/landing/FeaturesSection';
import { GamesCarouselSection } from '@/components/landing/GamesCarouselSection';
import { HeroSection } from '@/components/landing/HeroSection';
import { HowItWorksSection } from '@/components/landing/HowItWorksSection';
import { getServerUser } from '@/lib/auth/server';

import type { Metadata } from 'next';

// Metadata for SEO
export const metadata: Metadata = {
  title: 'MeepleAI - Il tuo assistente AI per giochi da tavolo',
  description:
    'Risposte immediate alle regole dei giochi da tavolo, in italiano. AI intelligente con citazioni dal manuale ufficiale. Sempre con te, mobile-first.',
  keywords: [
    'giochi da tavolo',
    'regole',
    'AI',
    'assistente',
    'italiano',
    'board games',
    'meeple',
    'intelligenza artificiale',
    'citazioni',
    'manuale',
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
    title: 'MeepleAI - Il tuo assistente AI per giochi da tavolo',
    description:
      'Risposte immediate alle regole dei giochi da tavolo con AI. Niente più discussioni, sempre citazioni dal manuale.',
    images: [
      {
        url: '/og-image.png',
        width: 1200,
        height: 630,
        alt: 'MeepleAI - Assistente AI per giochi da tavolo',
      },
    ],
  },
  twitter: {
    card: 'summary_large_image',
    site: '@MeepleAI',
    creator: '@MeepleAI',
    title: 'MeepleAI - Il tuo assistente AI per giochi da tavolo',
    description:
      'Risposte immediate alle regole dei giochi da tavolo, in italiano. Niente più discussioni!',
    images: ['/twitter-card.png'],
  },
  alternates: {
    canonical: 'https://meepleai.dev',
    languages: {
      'it-IT': 'https://meepleai.dev',
      'en-US': 'https://meepleai.dev/en',
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
  aggregateRating: {
    '@type': 'AggregateRating',
    ratingValue: '4.8',
    ratingCount: '1250',
  },
  description:
    'AI-powered board game rules assistant providing instant, accurate answers with official rulebook citations in Italian.',
  inLanguage: 'it-IT',
  featureList: [
    'Risposte immediate con AI',
    'Citazioni dal manuale ufficiale',
    'Migliaia di giochi disponibili',
    'Mobile-first design',
    'Supporto in italiano',
  ],
};

/**
 * Landing Page - Server Component
 *
 * Renders marketing landing page with hero, features, how-it-works, and footer.
 * All sections are server-rendered for optimal performance and SEO.
 *
 * Updated: Issue #2230 - Integrated PublicHeader for navigation
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

      {/* Main Landing Content */}
      {/* Note: <main> wrapper provided by PublicLayout - avoid nested landmarks (Issue #2234) */}
      <>
        {/* Hero Section */}
        <HeroSection />

        {/* Game Carousels Section (Issue #3588 - GC-003) */}
        <GamesCarouselSection />

        {/* Features Section */}
        <FeaturesSection />

        {/* How It Works Section */}
        <HowItWorksSection />

        {/* CTA Section (extracted from LandingFooter) */}
        <CallToActionSection />
      </>

      {/* Footer provided by (public)/layout.tsx → PublicLayout → PublicFooter */}
    </>
  );
}
