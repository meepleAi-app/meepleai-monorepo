'use client';

import { lazy, Suspense } from 'react';

import { ExternalLink, FileText, Gamepad2, MessageSquare, Wrench, X } from 'lucide-react';
import Link from 'next/link';

import { GameStatsPanel } from '@/components/library/game-table/GameStatsPanel';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useAgentKbDocs } from '@/hooks/queries/useAgentData';
import type { DrawerContent } from '@/lib/stores/gameTableDrawerStore';

// Lazy-load the embedded chat view to avoid pulling the full chat bundle eagerly
const EmbeddedChatView = lazy(() =>
  import('@/components/chat-unified/EmbeddedChatView').then(m => ({
    default: m.EmbeddedChatView,
  }))
);

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
// Sub-components for drawer content types
// ---------------------------------------------------------------------------

/** KB document list rendered inside the drawer */
function KbDocumentList({ gameId }: { gameId: string }) {
  const { data: docs, isLoading } = useAgentKbDocs(gameId);

  if (isLoading) {
    return (
      <div className="space-y-2">
        {[1, 2, 3].map(i => (
          <Skeleton key={i} className="h-10 w-full bg-white/10 rounded" />
        ))}
      </div>
    );
  }

  if (!docs || docs.length === 0) {
    return (
      <div className="text-[#8b949e] text-sm text-center py-6">
        <FileText className="h-8 w-8 mx-auto mb-2 opacity-50" />
        <p>Nessun documento nella Knowledge Base.</p>
        <p className="text-xs mt-1">Carica un PDF per iniziare.</p>
      </div>
    );
  }

  return (
    <ul className="space-y-2">
      {docs.map(doc => (
        <li
          key={doc.id}
          className="flex items-center gap-2 p-2 rounded bg-[#21262d] border border-[#30363d]"
        >
          <FileText className="h-4 w-4 text-[#8b949e] flex-shrink-0" />
          <span className="text-sm text-[#e6edf3] truncate flex-1">{doc.fileName}</span>
          {doc.status && <span className="text-xs text-[#8b949e] capitalize">{doc.status}</span>}
        </li>
      ))}
    </ul>
  );
}

// ---------------------------------------------------------------------------
// Content renderer (routing by discriminated union type)
// ---------------------------------------------------------------------------

function DrawerContentRenderer({ content }: { content: DrawerContent }) {
  switch (content.type) {
    case 'chat':
      return content.threadId ? (
        <Suspense fallback={<Skeleton className="h-64 w-full bg-white/10 rounded" />}>
          <EmbeddedChatView threadId={content.threadId} agentId={content.agentId} gameId="" />
        </Suspense>
      ) : (
        <div className="text-[#8b949e] text-sm text-center py-6">
          <MessageSquare className="h-8 w-8 mx-auto mb-2 opacity-50" />
          <p>Seleziona un thread dalla zona Conoscenza per avviare la chat.</p>
        </div>
      );
    case 'stats':
      return <GameStatsPanel gameId={content.gameId} />;
    case 'kb':
      return <KbDocumentList gameId={content.gameId} />;
    case 'toolkit':
      return (
        <div className="text-center py-6 space-y-3">
          <Wrench className="h-8 w-8 mx-auto text-[#8b949e] opacity-50" />
          <p className="text-[#8b949e] text-sm">
            Gestisci timer, dadi, segnapunti e altri strumenti di gioco.
          </p>
          <Button asChild variant="outline" size="sm" className="text-[#e6edf3] border-[#30363d]">
            <Link href={`/library/games/${content.gameId}/toolkit`}>
              <ExternalLink className="mr-2 h-3 w-3" />
              Apri Toolkit
            </Link>
          </Button>
        </div>
      );
    case 'document':
      return (
        <div className="text-center py-6 space-y-3">
          <FileText className="h-8 w-8 mx-auto text-[#8b949e] opacity-50" />
          <p className="text-[#8b949e] text-sm">
            Documento: <span className="text-[#e6edf3]">{content.documentId}</span>
          </p>
          <Button asChild variant="outline" size="sm" className="text-[#e6edf3] border-[#30363d]">
            <Link href={`/library/documents/${content.documentId}`}>
              <ExternalLink className="mr-2 h-3 w-3" />
              Visualizza documento
            </Link>
          </Button>
        </div>
      );
    case 'session':
      return (
        <div className="text-center py-6 space-y-3">
          <Gamepad2 className="h-8 w-8 mx-auto text-[#8b949e] opacity-50" />
          <p className="text-[#8b949e] text-sm">Sessione di gioco in corso.</p>
          <Button asChild variant="outline" size="sm" className="text-[#e6edf3] border-[#30363d]">
            <Link href={`/library/sessions/${content.sessionId}`}>
              <ExternalLink className="mr-2 h-3 w-3" />
              Vai alla sessione
            </Link>
          </Button>
        </div>
      );
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
