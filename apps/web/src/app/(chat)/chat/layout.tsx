/**
 * Chat Section Layout
 *
 * Desktop: 2-panel layout with persistent conversation list sidebar (w-80) + main content.
 * Mobile: single panel, children only (conversation list is page.tsx, thread is [threadId]).
 *
 * Carte in Mano — NavConfig removed, navigation handled by UserShell card system.
 */

import { type ReactNode } from 'react';

import { ChatConversationList } from '@/components/chat-unified/ChatConversationList';

export default function ChatSectionLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex h-full">
      {/* Conversation list sidebar — desktop only */}
      <aside className="hidden lg:flex lg:w-80 lg:flex-shrink-0 flex-col border-r border-border/40 overflow-y-auto bg-background/50">
        <ChatConversationList />
      </aside>

      {/* Main content — thread view or mobile conversation list */}
      <div className="flex-1 min-w-0">{children}</div>
    </div>
  );
}
