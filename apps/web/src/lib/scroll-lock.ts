/**
 * Shared scroll-lock utility.
 *
 * Uses a reference counter so multiple overlays (BottomSheet, FullScreenSearch, etc.)
 * can independently lock/unlock without racing on `document.body.style.overflow`.
 */
let count = 0;

export function lockScroll() {
  count++;
  if (count === 1) document.body.style.overflow = 'hidden';
}

export function unlockScroll() {
  count = Math.max(0, count - 1);
  if (count === 0) document.body.style.overflow = '';
}
