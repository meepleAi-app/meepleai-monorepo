'use client';

import { useChatPanelStore, type ChatGameContext } from '@/lib/stores/chat-panel-store';

/**
 * React hook for consuming the chat panel store.
 *
 * Returns a stable API: { isOpen, gameContext, open, close, setGameContext }.
 * Usage:
 *   const { isOpen, open, close } = useChatPanel();
 *   <button onClick={() => open({ id: 'azul', name: 'Azul', pdfCount: 3, kbStatus: 'ready' })}>
 *     Chiedi all'agente
 *   </button>
 *
 * Phase 5 will add URL state sync (?chat=open&gameId=X) in this hook.
 */
export function useChatPanel() {
  const isOpen = useChatPanelStore(s => s.isOpen);
  const gameContext = useChatPanelStore(s => s.gameContext);
  const open = useChatPanelStore(s => s.open);
  const close = useChatPanelStore(s => s.close);
  const setGameContext = useChatPanelStore(s => s.setGameContext);

  return {
    isOpen,
    gameContext,
    open,
    close,
    setGameContext,
  };
}

export type { ChatGameContext };
