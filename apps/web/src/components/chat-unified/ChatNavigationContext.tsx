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

import Link from 'next/link';

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

        const navLinks = getNavigationLinks('chat', {
          id: thread.id,
          gameId: thread.gameId ?? undefined,
        });
        setLinks(navLinks);
      } catch {
        // Navigation context is non-critical - silently fail
      }
    }

    loadThread();
    return () => {
      cancelled = true;
    };
  }, [threadId]);

  if (links.length === 0) return null;

  return (
    <div className="sticky top-0 z-10 flex items-center gap-2 border-b bg-card px-4 py-2">
      {links.map(link =>
        link.href ? (
          <Link
            key={link.entity}
            href={link.href}
            className="text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            {link.label}
          </Link>
        ) : null
      )}
    </div>
  );
}
