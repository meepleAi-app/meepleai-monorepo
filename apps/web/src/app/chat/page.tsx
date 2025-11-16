/**
 * App Router Chat Page (Server Component)
 *
 * This is a Server Component wrapper that imports the existing client-side
 * chat page component. This allows us to use the App Router while
 * maintaining compatibility with the existing Pages Router implementation.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 *
 * Benefits of Server Components for Chat Page:
 * - Reduced initial JavaScript bundle
 * - Faster time to interactive
 * - Better performance on mobile devices
 *
 * @see https://nextjs.org/docs/app/building-your-application/routing/pages-and-layouts
 */

import { Metadata } from 'next';
import ChatPage from '@/components/pages/ChatPage';

export const metadata: Metadata = {
  title: 'Chat - MeepleAI',
  description: 'Ask questions about board game rules and get instant AI-powered answers with source citations.',
  keywords: ['chat', 'AI', 'board game rules', 'question answering', 'semantic search'],
  openGraph: {
    title: 'Chat - MeepleAI',
    description: 'Ask questions about board game rules and get instant AI-powered answers.',
    type: 'website',
    locale: 'it_IT',
  },
  robots: {
    index: false, // Chat page should not be indexed
    follow: true,
  },
};

/**
 * Chat Page - Server Component
 *
 * This Server Component wrapper imports the existing client-side
 * chat interface, allowing gradual migration to App Router patterns.
 *
 * Future optimization opportunities:
 * - Move static UI elements to Server Components
 * - Implement streaming for chat responses
 * - Add suspense boundaries for better loading states
 */
export default function Page() {
  return <ChatPage />;
}
