/**
 * App Router Home Page (Server Component)
 *
 * This is a Server Component wrapper that imports the existing client-side
 * landing page component. This allows us to use the App Router while
 * maintaining compatibility with the existing Pages Router implementation.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 *
 * Benefits of Server Components:
 * - Reduced JavaScript bundle size (~10% reduction target)
 * - Improved initial page load performance
 * - Better SEO with server-side rendering
 *
 * @see https://nextjs.org/docs/app/building-your-application/routing/pages-and-layouts
 */

import { Metadata } from 'next';
import HomePage from '@/components/pages/HomePage';

export const metadata: Metadata = {
  title: 'MeepleAI - AI-Powered Board Game Rules Assistant',
  description: 'Never argue about rules again. Get instant, accurate answers from any game\'s rulebook with AI-powered semantic search.',
  keywords: ['board games', 'rules', 'AI', 'assistant', 'semantic search', 'meeple', 'gaming'],
  openGraph: {
    title: 'MeepleAI - AI-Powered Board Game Rules Assistant',
    description: 'Get instant, accurate answers from any board game rulebook with AI-powered semantic search.',
    type: 'website',
    locale: 'it_IT',
  },
  twitter: {
    card: 'summary_large_image',
    title: 'MeepleAI - AI-Powered Board Game Rules Assistant',
    description: 'Never argue about rules again. Get instant, accurate answers from any game\'s rulebook.',
  },
};

/**
 * Home Page - Server Component
 *
 * This Server Component wrapper imports the existing client-side
 * landing page, allowing gradual migration to App Router patterns.
 */
export default function Page() {
  return <HomePage />;
}
