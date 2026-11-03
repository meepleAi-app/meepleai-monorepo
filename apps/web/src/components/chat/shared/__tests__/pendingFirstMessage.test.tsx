/**
 * #4740 — la domanda di un Quick Start passa dalla pagina di avvio al thread.
 *
 * Il backend non elabora `InitialMessage`: la domanda la deve spedire una vista del thread, aperta
 * dopo il `router.push`. Qui si prova il passaggio di mano (una sola lettura, mai due invii) e la
 * scelta della vista: la pagina del thread monta `ChatMobile` e `ChatThreadView` insieme e ne
 * nasconde una col CSS, quindi solo quella visibile può spedire.
 */

import { renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  stashPendingFirstMessage,
  takePendingFirstMessage,
  usePendingFirstMessage,
} from '../pendingFirstMessage';

function setViewport(desktop: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: desktop && query === '(min-width: 1024px)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

beforeEach(() => {
  window.sessionStorage.clear();
  setViewport(true);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('stashPendingFirstMessage / takePendingFirstMessage', () => {
  it('hands the message over once: a second read finds nothing', () => {
    stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

    expect(takePendingFirstMessage('thread-1')).toBe('Come si gioca a Catan?');
    expect(takePendingFirstMessage('thread-1')).toBeNull();
  });

  it('keeps the message of one thread out of another', () => {
    stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

    expect(takePendingFirstMessage('thread-2')).toBeNull();
    expect(takePendingFirstMessage('thread-1')).toBe('Come si gioca a Catan?');
  });

  it('finds nothing when nothing was stashed', () => {
    expect(takePendingFirstMessage('thread-1')).toBeNull();
  });

  it('survives storage that refuses to be read or written: no message, no crash', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?')).not.toThrow();
    expect(takePendingFirstMessage('thread-1')).toBeNull();
  });
});

describe('usePendingFirstMessage', () => {
  function mount(opts: {
    view: 'desktop' | 'mobile';
    ready: boolean;
    threadId?: string;
    send?: (message: string) => void;
  }) {
    const send = opts.send ?? vi.fn();
    const hook = renderHook(
      (props: { ready: boolean }) =>
        usePendingFirstMessage({
          threadId: opts.threadId ?? 'thread-1',
          view: opts.view,
          ready: props.ready,
          send,
        }),
      { initialProps: { ready: opts.ready } }
    );
    return { send, ...hook };
  }

  it('sends the stashed question once the thread is ready', () => {
    stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

    const { send } = mount({ view: 'desktop', ready: true });

    expect(send).toHaveBeenCalledTimes(1);
    expect(send).toHaveBeenCalledWith('Come si gioca a Catan?');
  });

  it('waits while the thread is still loading, and keeps the question for later', () => {
    stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

    const { send, rerender } = mount({ view: 'desktop', ready: false });
    expect(send).not.toHaveBeenCalled();

    rerender({ ready: true });
    expect(send).toHaveBeenCalledTimes(1);
    expect(send).toHaveBeenCalledWith('Come si gioca a Catan?');
  });

  it('does not send the same question again on a later render', () => {
    stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

    const { send, rerender } = mount({ view: 'desktop', ready: true });
    rerender({ ready: true });
    rerender({ ready: false });
    rerender({ ready: true });

    expect(send).toHaveBeenCalledTimes(1);
  });

  it('sends nothing when no question was stashed', () => {
    const { send } = mount({ view: 'desktop', ready: true });

    expect(send).not.toHaveBeenCalled();
  });

  it('sends only the question stashed for its own thread', () => {
    stashPendingFirstMessage('thread-2', 'Come si gioca a Catan?');

    const { send } = mount({ view: 'desktop', ready: true, threadId: 'thread-1' });

    expect(send).not.toHaveBeenCalled();
    expect(takePendingFirstMessage('thread-2')).toBe('Come si gioca a Catan?');
  });

  describe('the page mounts both views and hides one', () => {
    it('on a wide screen the desktop view sends and the mobile view leaves the question alone', () => {
      setViewport(true);
      stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

      const mobile = mount({ view: 'mobile', ready: true });
      expect(mobile.send).not.toHaveBeenCalled();

      const desktop = mount({ view: 'desktop', ready: true });
      expect(desktop.send).toHaveBeenCalledWith('Come si gioca a Catan?');
      expect(mobile.send).not.toHaveBeenCalled();
    });

    it('on a narrow screen the mobile view sends and the desktop view leaves the question alone', () => {
      setViewport(false);
      stashPendingFirstMessage('thread-1', 'Come si gioca a Catan?');

      const desktop = mount({ view: 'desktop', ready: true });
      expect(desktop.send).not.toHaveBeenCalled();

      const mobile = mount({ view: 'mobile', ready: true });
      expect(mobile.send).toHaveBeenCalledWith('Come si gioca a Catan?');
      expect(desktop.send).not.toHaveBeenCalled();
    });
  });
});
