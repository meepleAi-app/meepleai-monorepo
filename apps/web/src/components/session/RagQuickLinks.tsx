'use client';

import { useState, useEffect } from 'react';

import { BookOpen } from 'lucide-react';

import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { useChatPanel } from '@/hooks/useChatPanel';
import { api } from '@/lib/api';
import type { QuickLink } from '@/lib/api/clients/knowledgeBaseClient';
import { cn } from '@/lib/utils';

interface RagQuickLinksProps {
  gameId: string | null | undefined;
  className?: string;
}

export function RagQuickLinks({ gameId, className }: RagQuickLinksProps) {
  const [links, setLinks] = useState<QuickLink[]>([]);
  const [selected, setSelected] = useState<QuickLink | null>(null);
  const { open: openChat } = useChatPanel();

  useEffect(() => {
    if (!gameId) return;
    api.knowledgeBase
      .getQuickLinks(gameId)
      .then(data => {
        setLinks(data.slice(0, 4));
      })
      .catch(err => {
        console.error('[RagQuickLinks] Failed to fetch quick links:', err);
      });
  }, [gameId]);

  if (!gameId || links.length === 0) return null;

  return (
    <div data-testid="rag-quick-links" className={cn('space-y-2', className)}>
      <h4 className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
        <BookOpen className="h-3.5 w-3.5" aria-hidden="true" />
        Dalla Knowledge Base
      </h4>

      <div className="flex flex-wrap gap-2">
        {links.map(link => (
          <button
            key={link.id}
            type="button"
            onClick={() => setSelected(link)}
            className={cn(
              'px-3 py-1.5 rounded-full text-xs font-medium',
              'bg-amber-500/10 text-amber-600 dark:text-amber-400',
              'border border-amber-500/20 hover:bg-amber-500/20 transition-colors'
            )}
          >
            {link.title}
          </button>
        ))}
      </div>

      <BottomSheet
        open={!!selected}
        onOpenChange={open => {
          if (!open) setSelected(null);
        }}
        title={selected?.title ?? ''}
      >
        <div className="space-y-4 py-2">
          <p className="text-sm text-muted-foreground leading-relaxed">{selected?.snippet}</p>
          <button
            type="button"
            onClick={() => {
              openChat();
              setSelected(null);
            }}
            className={cn(
              'w-full py-3 rounded-xl text-sm font-semibold',
              'bg-amber-600 text-white hover:bg-amber-700 transition-colors'
            )}
          >
            Chiedi all&apos;agente
          </button>
        </div>
      </BottomSheet>
    </div>
  );
}
