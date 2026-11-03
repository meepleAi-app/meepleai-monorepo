/**
 * `pendingFirstMessage` — hands the first question of a new chat from the start page to the thread.
 *
 * Issue #4740: a Quick Start on `/chat/new` sent its question as `initialMessage` to
 * `POST /chat-threads`, but the backend does not process it (`CreateChatThreadCommand` declares it
 * "accepted for API contract") and no thread view recovered it, so the thread opened empty. The
 * answer streams from the client (`qaStream`), so a thread view has to send the question; this is
 * how the start page tells it what to send.
 *
 * sessionStorage and not the URL: the question is read once and removed, so a reload or the back
 * button cannot ask it twice, and nothing lingers in the address bar or the history.
 *
 * Module boundary: lives under `chat/shared/**` — MUST NOT import from `chat-unified/**` or
 * `chat/panel/**` (enforced by ESLint).
 */

import { useEffect, useRef } from 'react';

import { useMediaQuery } from '@/hooks/useMediaQuery';

const KEY_PREFIX = 'meepleai:chat:first-message:';

/** Tailwind `lg`: from here up the page shows `ChatThreadView`, below it `ChatMobile`. */
const DESKTOP_VIEW_QUERY = '(min-width: 1024px)';

/**
 * Leaves `message` for the thread `threadId` to ask when it opens. Storage that refuses the write
 * (private window, blocked site data) loses the question and nothing else: the thread opens empty,
 * as it did before.
 */
export function stashPendingFirstMessage(threadId: string, message: string): void {
  try {
    window.sessionStorage.setItem(`${KEY_PREFIX}${threadId}`, message);
  } catch {
    // Nothing to hand over; see above.
  }
}

/** Reads and removes the question left for `threadId`, or `null` if there is none. */
export function takePendingFirstMessage(threadId: string): string | null {
  try {
    const key = `${KEY_PREFIX}${threadId}`;
    const message = window.sessionStorage.getItem(key);
    if (message === null) return null;
    window.sessionStorage.removeItem(key);
    return message;
  } catch {
    return null;
  }
}

export interface UsePendingFirstMessageOptions {
  threadId: string;
  /** Which of the two thread views is calling. */
  view: 'desktop' | 'mobile';
  /**
   * The thread is loaded, belongs to `threadId`, has a game and no messages. Until then the question
   * stays in storage.
   */
  ready: boolean;
  /** Asks the question in the thread: the view's own send path. */
  send: (message: string) => void;
}

/**
 * Asks the question the start page left for this thread, once, from the view that is on screen.
 *
 * The thread page mounts `ChatMobile` and `ChatThreadView` together and hides one with CSS, each
 * with its own message state. A question sent from the hidden one would never be seen, so the view
 * that is not on screen leaves it in storage for the other.
 */
export function usePendingFirstMessage({
  threadId,
  view,
  ready,
  send,
}: UsePendingFirstMessageOptions): void {
  const isDesktop = useMediaQuery(DESKTOP_VIEW_QUERY);
  const isOnScreen = view === 'desktop' ? isDesktop : !isDesktop;

  // The latest `send` without making it a dependency: the question goes out when the thread becomes
  // ready, not every time the view's callbacks are recreated.
  const sendRef = useRef(send);
  sendRef.current = send;

  useEffect(() => {
    if (!ready || !isOnScreen) return;
    const message = takePendingFirstMessage(threadId);
    if (message) sendRef.current(message);
  }, [ready, isOnScreen, threadId]);
}
