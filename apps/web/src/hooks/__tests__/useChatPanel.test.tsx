import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

import { useChatPanel } from '../useChatPanel';

describe('useChatPanel', () => {
  beforeEach(() => {
    useChatPanelStore.getState().close();
    useChatPanelStore.getState().clearGameContext();
  });

  it('exposes isOpen=false by default', () => {
    const { result } = renderHook(() => useChatPanel());
    expect(result.current.isOpen).toBe(false);
    expect(result.current.gameContext).toBeNull();
  });

  it('open() updates isOpen to true', () => {
    const { result } = renderHook(() => useChatPanel());
    act(() => {
      result.current.open();
    });
    expect(result.current.isOpen).toBe(true);
  });

  it('open(gameContext) sets context and opens', () => {
    const { result } = renderHook(() => useChatPanel());
    act(() => {
      result.current.open({
        id: 'azul',
        name: 'Azul',
        pdfCount: 3,
        kbStatus: 'ready',
      });
    });
    expect(result.current.isOpen).toBe(true);
    expect(result.current.gameContext?.name).toBe('Azul');
  });

  it('close() sets isOpen to false', () => {
    const { result } = renderHook(() => useChatPanel());
    act(() => {
      result.current.open();
      result.current.close();
    });
    expect(result.current.isOpen).toBe(false);
  });

  it('setGameContext() updates context without closing', () => {
    const { result } = renderHook(() => useChatPanel());
    act(() => {
      result.current.open();
      result.current.setGameContext({
        id: 'wings',
        name: 'Wingspan',
        pdfCount: 2,
        kbStatus: 'ready',
      });
    });
    expect(result.current.isOpen).toBe(true);
    expect(result.current.gameContext?.name).toBe('Wingspan');
  });
});
