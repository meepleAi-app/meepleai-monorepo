'use client';

import React from 'react';

import { cn } from '@/lib/utils';

import type { AgentDetailData } from '../types';

export interface AgentChatDrawerLayoutProps {
  data: AgentDetailData;
  className?: string;
  'data-testid'?: string;
}

export const AgentChatDrawerLayout = React.memo(function AgentChatDrawerLayout({
  data,
  className,
  'data-testid': testId,
}: AgentChatDrawerLayoutProps) {
  return (
    <div
      className={cn('flex h-full w-full overflow-hidden', className)}
      data-testid={testId ?? 'agent-chat-drawer-layout'}
    >
      <div data-testid="agent-chat-sidebar" className="w-[220px] shrink-0">
        {data.gameName ? <span>{data.gameName}</span> : <span data-testid="no-game-placeholder" />}
        <button data-testid="new-chat-button">+ Nuova chat</button>
        <p>CHAT RECENTI</p>
        <p>KNOWLEDGE BASE</p>
      </div>
      <div data-testid="agent-chat-area" className="flex-1" />
    </div>
  );
});
