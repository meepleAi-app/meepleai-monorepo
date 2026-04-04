/**
 * Chat Thread Page - /chat/[threadId] (Issue #4364)
 *
 * Split view chat with messages + info panel.
 * Loads thread by ID from URL params.
 */

'use client';

import { use } from 'react';

import dynamic from 'next/dynamic';

import { ChatMobile } from '@/components/chat-unified/ChatMobile';
import { ChatNavigationContext } from '@/components/chat-unified/ChatNavigationContext';

const ChatThreadView = dynamic(
  () =>
    import('@/components/chat-unified/ChatThreadView').then(mod => ({
      default: mod.ChatThreadView,
    })),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-dvh items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-500 mx-auto mb-4" />
          <p className="text-muted-foreground font-nunito">Caricamento chat...</p>
        </div>
      </div>
    ),
  }
);

export default function ChatThreadPage({ params }: { params: Promise<{ threadId: string }> }) {
  const { threadId } = use(params);

  return (
    <>
      <ChatNavigationContext threadId={threadId} />
      {/* Mobile */}
      <div className="lg:hidden h-dvh">
        <ChatMobile threadId={threadId} />
      </div>
      {/* Desktop */}
      <div className="hidden lg:block h-dvh">
        <ChatThreadView threadId={threadId} />
      </div>
    </>
  );
}
