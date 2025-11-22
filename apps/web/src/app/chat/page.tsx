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

import ChatPage from '@/components/pages/ChatPage';

/**
 * Chat Page - Client Component
 *
 * This Client Component wrapper imports the existing chat interface,
 * allowing us to use the App Router while maintaining compatibility
 * with client-side state management.
 *
 * Future optimization opportunities:
 * - Implement streaming for chat responses
 * - Add suspense boundaries for better loading states
 */
export default function Page() {
  return <ChatPage />;
}
