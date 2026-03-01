/**
 * New Chat Page - /chat/new (Issue #4363)
 *
 * Welcome page for creating new conversations.
 * Shows game + agent selection with MeepleCard grids.
 *
 * Supports query params:
 * - ?game={id} - Pre-selects a game
 * - ?agent={type} - Pre-selects an agent type
 */

'use client';

import { Suspense } from 'react';

import dynamicImport from 'next/dynamic';

const NewChatView = dynamicImport(
  () => import('@/components/chat-unified/NewChatView').then(mod => ({ default: mod.NewChatView })),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-dvh items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-500 mx-auto mb-4" />
          <p className="text-muted-foreground font-nunito">Caricamento...</p>
        </div>
      </div>
    ),
  }
);

export default function NewChatPage() {
  return (
    <Suspense>
      <NewChatView />
    </Suspense>
  );
}
