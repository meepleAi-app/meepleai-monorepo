/**
 * ChatNavigationContext - Thin navigation bar for chat detail page.
 *
 * Fetches thread metadata and renders entity navigation links
 * (Game, Agent, Session) above the chat interface.
 *
 * @see Issue #4696
 */

'use client';

import { useEffect, useState } from 'react';

import { CardNavigationFooter } from '@/components/ui/data-display/meeple-card-features/CardNavigationFooter';
import { getNavigationLinks, type ResolvedNavigationLink } from '@/config/entity-navigation';
import { api } from '@/lib/api';

interface ChatNavigationContextProps {
  threadId: string;
}

export function ChatNavigationContext({ threadId }: ChatNavigationContextProps) {
  const [links, setLinks] = useState<ResolvedNavigationLink[]>([]);

  useEffect(() => {
    let cancelled = false;

    async function loadThread() {
      try {
        const thread = await api.chat.getThreadById(threadId);
        if (cancelled || !thread) return;

        const navLinks = getNavigationLinks('chatSession', {
          id: thread.id,
          gameId: thread.gameId ?? undefined,
        });
        setLinks(navLinks);
      } catch {
        // Navigation context is non-critical - silently fail
      }
    }

    loadThread();
    return () => { cancelled = true; };
  }, [threadId]);

  if (links.length === 0) return null;

  return (
    <div className="sticky top-0 z-10">
      <CardNavigationFooter
        links={links}
        className="rounded-none border-t-0 border-x-0"
      />
    </div>
  );
}
