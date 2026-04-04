/**
 * Chat Route Group Layout
 *
 * Uses UserShell for the chat experience.
 * (chat) is a sibling of (authenticated), so it needs its own shell wrapper.
 */

import { type ReactNode } from 'react';

import { ChatContextBar } from '@/components/chat-unified/ChatContextBar';
import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { UserShell } from '@/components/layout/UserShell';

export default function ChatLayout({ children }: { children: ReactNode }) {
  return (
    <UserShell>
      <ContextBarRegistrar>
        <ChatContextBar />
      </ContextBarRegistrar>
      {children}
    </UserShell>
  );
}
