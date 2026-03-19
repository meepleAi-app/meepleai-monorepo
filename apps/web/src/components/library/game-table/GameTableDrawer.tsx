'use client';

import { X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import type { DrawerContent } from '@/lib/stores/gameTableDrawerStore';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface GameTableDrawerProps {
  content: DrawerContent;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const DRAWER_TITLES: Record<DrawerContent['type'], string> = {
  chat: 'Chat AI',
  stats: 'Statistiche',
  kb: 'Knowledge Base',
  toolkit: 'Toolkit',
  document: 'Documento',
  session: 'Sessione',
};

const DRAWER_ICONS: Record<DrawerContent['type'], string> = {
  chat: '💬',
  stats: '📊',
  kb: '📚',
  toolkit: '🧰',
  document: '📄',
  session: '🎲',
};

// ---------------------------------------------------------------------------
// Content renderer (routing by discriminated union type)
// ---------------------------------------------------------------------------

function DrawerContentRenderer({ content }: { content: DrawerContent }) {
  switch (content.type) {
    case 'chat':
      return content.threadId ? (
        <div className="text-[#e6edf3]">Chat thread: {content.threadId}</div>
      ) : (
        <div className="text-[#8b949e]">Select a chat thread for agent {content.agentId}</div>
      );
    case 'stats':
      return <div className="text-[#8b949e]">Statistics panel — gameId: {content.gameId}</div>;
    case 'kb':
      return <div className="text-[#8b949e]">Knowledge Base — {content.gameId}</div>;
    case 'toolkit':
      return <div className="text-[#8b949e]">Toolkit — {content.gameId}</div>;
    case 'document':
      return <div className="text-[#8b949e]">Document — {content.documentId}</div>;
    case 'session':
      return <div className="text-[#8b949e]">Session — {content.sessionId}</div>;
    default:
      return null;
  }
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function GameTableDrawer({ content, onClose }: GameTableDrawerProps) {
  return (
    <div className="flex flex-col h-full">
      {/* Sticky header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-[#30363d] bg-[#161b22] flex-shrink-0">
        <div className="flex items-center gap-2">
          <span className="text-sm">{DRAWER_ICONS[content.type]}</span>
          <h2 className="text-base font-semibold text-[#e6edf3] font-quicksand">
            {DRAWER_TITLES[content.type]}
          </h2>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClose}
          aria-label="Chiudi"
          className="text-[#8b949e] hover:text-[#e6edf3]"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Scrollable content area */}
      <div className="flex-1 overflow-y-auto p-4">
        <DrawerContentRenderer content={content} />
      </div>
    </div>
  );
}
