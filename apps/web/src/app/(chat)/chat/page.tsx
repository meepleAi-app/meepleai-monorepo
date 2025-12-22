/**
 * App Router Chat Page (Client Component)
 *
 * This is a Client Component wrapper that imports the existing client-side
 * chat page component. This allows us to use the App Router while
 * maintaining compatibility with the existing Pages Router implementation.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 *
 * Note: Uses 'use client' directive because ChatPage depends on
 * client-side hooks and providers.
 *
 * @see https://nextjs.org/docs/app/building-your-application/routing/pages-and-layouts
 */

'use client';

import dynamic from 'next/dynamic';

// Issue #1817: Dynamic import with ssr:false to prevent DOMMatrix SSR errors
// pdfjs-dist (dependency) → @napi-rs/canvas uses DOMMatrix which isn't available in Node.js
// This prevents Next.js from attempting static generation during build
const ChatPage = dynamic(() => import('@/components/pages/ChatPage'), {
  ssr: false,
  loading: () => (
    <div className="flex h-screen items-center justify-center">
      <div className="text-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto mb-4"></div>
        <p>Loading chat...</p>
      </div>
    </div>
  ),
});

/**
 * Chat Page - Client Component
 *
 * This Client Component wrapper dynamically imports the chat interface
 * to prevent SSR issues with pdfjs-dist's DOMMatrix dependency.
 *
 * Future optimization opportunities:
 * - Implement streaming for chat responses
 * - Add suspense boundaries for better loading states
 */
export default function Page() {
  return <ChatPage />;
}
