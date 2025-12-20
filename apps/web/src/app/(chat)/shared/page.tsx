/**
 * Shared Chat Page (Issue #2052)
 *
 * Public route for viewing shared chat threads via share link token.
 * No authentication required - access controlled by JWT token in URL.
 *
 * Route: /shared/chat?token={jwt_token}
 *
 * SSR disabled to support client-side URL parameter reading.
 */

'use client';

import { Suspense } from 'react';

import { Loader2 } from 'lucide-react';

import { SharedChatView } from '@/components/pages/SharedChatView';

function LoadingFallback() {
  return (
    <div className="flex h-screen items-center justify-center">
      <div className="text-center">
        <Loader2 className="animate-spin h-12 w-12 text-primary mx-auto mb-4" />
        <p className="text-muted-foreground">Loading shared chat...</p>
      </div>
    </div>
  );
}

export default function Page() {
  return (
    <Suspense fallback={<LoadingFallback />}>
      <SharedChatView />
    </Suspense>
  );
}
