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

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { getNavigationLinks, type ResolvedNavigationLink } from '@/config/entity-navigation';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';

interface ChatNavigationContextProps {
  threadId: string;
}

// Entity-type → i18n key map. Keeps the `entity-navigation` config translation-
// free (labels stay as EN fallbacks) while the chat surface localizes labels
// via the translation layer. New entries map to `common.entity.*` keys.
const ENTITY_LABEL_KEY: Partial<Record<MeepleEntityType, string>> = {
  game: 'common.entity.game',
  agent: 'common.entity.agent',
  session: 'common.entity.session',
  kb: 'common.entity.kb',
  player: 'common.entity.player',
  chat: 'common.entity.chat',
  event: 'common.entity.event',
  toolkit: 'common.entity.toolkit',
};

export function ChatNavigationContext({ threadId }: ChatNavigationContextProps) {
  const { t } = useTranslation();
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
      {links.map(link => {
        if (!link.href) return null;
        const labelKey = ENTITY_LABEL_KEY[link.entity];
        const label = labelKey ? t(labelKey) : link.label;
        return (
          <Link
            key={link.entity}
            href={link.href}
            className="text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            {label}
          </Link>
        );
      })}
    </div>
  );
}
