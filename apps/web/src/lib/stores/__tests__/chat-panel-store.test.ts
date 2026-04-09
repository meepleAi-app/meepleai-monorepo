import { describe, it, expect, beforeEach } from 'vitest';

import { useChatPanelStore, type ChatGameContext } from '../chat-panel-store';

describe('chat-panel-store', () => {
  beforeEach(() => {
    useChatPanelStore.getState().close();
  });

  it('starts closed with no game context', () => {
    const state = useChatPanelStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.gameContext).toBeNull();
  });

  it('open() sets isOpen to true', () => {
    useChatPanelStore.getState().open();
    expect(useChatPanelStore.getState().isOpen).toBe(true);
  });

  it('open(gameContext) sets isOpen and gameContext', () => {
    const ctx: ChatGameContext = {
      id: 'azul',
      name: 'Azul',
      year: 2017,
      pdfCount: 3,
      kbStatus: 'ready',
    };
    useChatPanelStore.getState().open(ctx);
    const state = useChatPanelStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.gameContext).toEqual(ctx);
  });

  it('close() resets isOpen but keeps gameContext', () => {
    useChatPanelStore.getState().open({
      id: 'azul',
      name: 'Azul',
      pdfCount: 3,
      kbStatus: 'ready',
    });
    useChatPanelStore.getState().close();
    const state = useChatPanelStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.gameContext).not.toBeNull();
  });

  it('setGameContext() updates the game context without closing', () => {
    useChatPanelStore.getState().open();
    useChatPanelStore.getState().setGameContext({
      id: 'wings',
      name: 'Wingspan',
      pdfCount: 2,
      kbStatus: 'ready',
    });
    const state = useChatPanelStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.gameContext?.name).toBe('Wingspan');
  });

  it('clearGameContext() removes the context without closing', () => {
    useChatPanelStore.getState().open({
      id: 'azul',
      name: 'Azul',
      pdfCount: 3,
      kbStatus: 'ready',
    });
    useChatPanelStore.getState().clearGameContext();
    expect(useChatPanelStore.getState().gameContext).toBeNull();
    expect(useChatPanelStore.getState().isOpen).toBe(true);
  });
});
